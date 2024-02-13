using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request, AppDbContext dbContext) =>
{
    if (request.Tipo != 'd' && request.Tipo != 'c' || request.Valor <= 0 || request.Descricao.Length < 1)
    {
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    var cliente = AppDbContext.GetCliente(dbContext, id);

    if (cliente is null) return Results.StatusCode(StatusCodes.Status404NotFound);

    if (request.Tipo == 'c')
    {
        cliente.Saldo += request.Valor;
    }
    else if (request.Tipo == 'd')
    {
        if (cliente.Saldo - request.Valor < -cliente.Limite)
        {
            return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
        }

        cliente.Saldo -= request.Valor;
    }

    await dbContext.SaveChangesAsync();

    return Results.Ok(new
    {
        Limite = cliente.Limite,
        Saldo = cliente.Saldo,
    });

});

app.MapGet("/clientes/{id}/extrato", (int id, AppDbContext dbContext) =>
{
    var cliente = AppDbContext.GetClienteAndTrasacoesWithLatestTransactions(dbContext, id);

    if (cliente is null) return Results.StatusCode(StatusCodes.Status404NotFound);

    var saldoTotal = cliente.Saldo;
    var dataExtrato = DateTime.UtcNow;
    var limite = cliente.Limite;

    var ultimasTransacoes = cliente.Transacoes
        .Select(t => new
        {
            valor = t.Valor,
            tipo = t.Tipo,
            descricao = t.Descricao,
            realizada_em = t.Realizado_Em
        })
        .ToList();

    var extrato = new
    {
        saldo = new
        {
            total = saldoTotal,
            data_extrato = dataExtrato,
            limite = limite
        },
        ultimas_transacoes = ultimasTransacoes
    };

    return Results.Ok(extrato);
});

await app.RunAsync("http://0.0.0.0:80");

public record TransacaoPayload(int Valor, char Tipo, string Descricao);
public class Cliente
{
    public int Id { get; set; }
    public int Saldo { get; set; }
    public int Limite { get; set; }
    public virtual IEnumerable<Transacao> Transacoes { get; set; } = Enumerable.Empty<Transacao>();
}

public class Transacao
{
    public required int Id { get; set; }
    public required int Valor { get; set; }
    public required char Tipo { get; set; }
    public required string Descricao { get; set; }
    public required DateTime Realizado_Em { get; set; } = DateTime.Now;
    public required int ClienteId { get; set; }
    public required virtual Cliente Cliente { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Transacao> Transacoes { get; set; }

    public static Func<AppDbContext, int, Cliente?> GetCliente =
    EF.CompileQuery((AppDbContext context, int id) =>
        context.Clientes.FirstOrDefault(c => c.Id == id));

    public static Func<AppDbContext, int, Cliente?> GetClienteAndTrasacoesWithLatestTransactions =
        EF.CompileQuery((AppDbContext context, int id) =>
            context.Clientes
                .Where(c => c.Id == id)
                .Select(c => new Cliente
                {
                    Id = c.Id,
                    Saldo = c.Saldo,
                    Limite = c.Limite,
                    Transacoes = c.Transacoes
                        .OrderByDescending(t => t.Realizado_Em)
                        .Take(10)
                })
                .FirstOrDefault());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("cliente");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Saldo).HasColumnName("saldo");
            entity.Property(e => e.Limite).HasColumnName("limite");
        });

        modelBuilder.Entity<Transacao>(entity =>
        {
            entity.ToTable("transacao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(1);
            entity.Property(e => e.Descricao).HasColumnName("descricao").HasMaxLength(255);
            entity.Property(e => e.Realizado_Em).HasColumnName("realizado_em");
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");

            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Transacoes)
                .HasForeignKey(e => e.ClienteId);
        });
    }
}