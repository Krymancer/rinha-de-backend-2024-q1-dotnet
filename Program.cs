using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request, AppDbContext dbContext) =>
{
    if (request.Tipo != 'd' && request.Tipo != 'c') return Results.BadRequest("Tipo de transação inválido");
    if (request.Valor <= 0) return Results.BadRequest("Valor da transação inválido");
    if (request.Descricao.Length < 1) return Results.BadRequest("Descrição da transação inválida");

    var cliente = await dbContext.Clientes.FindAsync(id);

    if (cliente == null)
    {
        return Results.NotFound("Cliente não encontrado");
    }

    int novoSaldo = 0;

    if (request.Tipo == 'c')
    {
        cliente.Saldo += request.Valor;
        novoSaldo = cliente.Saldo;
    }
    else if (request.Tipo == 'd')
    {
        if (cliente.Saldo - request.Valor < -cliente.Limite)
        {
            return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
        }

        cliente.Saldo -= request.Valor;
        novoSaldo = cliente.Saldo;
    }

    await dbContext.SaveChangesAsync();

    return Results.Ok(new
    {
        Limite = cliente.Limite,
        Saldo = novoSaldo
    });

});

app.MapGet("/clientes/{id}/extrato", async (int id, AppDbContext dbContext) =>
{
    var cliente = await dbContext.Clientes
        .Include(c => c.Transacoes)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (cliente == null)
    {
        return Results.NotFound("Cliente não encontrado");
    }

    var saldoTotal = cliente.Saldo;
    var dataExtrato = DateTime.UtcNow;
    var limite = cliente.Limite;

    var ultimasTransacoes = cliente.Transacoes
        .OrderByDescending(t => t.Realizado_Em)
        .Take(10)
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
public record TransacaoResponse(int limite, int saldo);

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