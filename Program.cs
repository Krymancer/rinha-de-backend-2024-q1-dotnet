using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

string connectionString = "Host=localhost;Port=5432;Database=db;Username=user;Password=password;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=10;";
var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});

var app = builder.Build();

List<NpgsqlConnection> connections = new List<NpgsqlConnection>();
for (int i = 0; i < 10; i++)
{
    var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    connections.Add(conn);
}

foreach (var conn in connections)
{
    conn.Close();
}

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    if (
        request.Tipo != 'd' && request.Tipo != 'c' ||
        request.Valor <= 0 || !(request.Valor % 1 == 0) ||
        request.Descricao is null || request.Descricao.Length < 1 || request.Descricao.Length > 10
        )
    {
        return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    }
    

    using (var connection = new NpgsqlConnection(connectionString))
    {

        var update = request.Tipo == 'c' 
        ? "UPDATE cliente SET saldo = saldo + @valor WHERE id = @id RETURNING saldo, limite"
        : "UPDATE cliente SET saldo = saldo - @valor WHERE id = @id AND saldo - @valor >= - limite RETURNING saldo, limite";
        
        var sql = $@"WITH updated AS ({update})
                      INSERT INTO transacao (valor, tipo, descricao, realizado_em, cliente_id)
                      VALUES (@valor, @tipo, @descricao, NOW(), @id)
                      RETURNING (SELECT (saldo, limite) FROM updated);
          ";

        await connection.OpenAsync().ConfigureAwait(false);

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("valor", (int)request.Valor);
        command.Parameters.AddWithValue("tipo", request.Tipo);
        command.Parameters.AddWithValue("descricao", request.Descricao);

        await command.PrepareAsync().ConfigureAwait(false);
        
        var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync().ConfigureAwait(false)) throw new InvalidOperationException();

        try {
          var record = reader.GetFieldValue<object[]>(0);
          return Results.Ok(new TransacaoResponse((int)record[0], (int)record[1]));
         } catch(Exception) {
          return Results.UnprocessableEntity();
        }
    }
});

app.MapGet("/clientes/{id}/extrato", async (int id) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    using (var connection = new NpgsqlConnection(connectionString))
    {
        await connection.OpenAsync().ConfigureAwait(false);
        
        string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
        
        var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("id", id);
        await command.PrepareAsync().ConfigureAwait(false);

        var read = await command.ExecuteReaderAsync().ConfigureAwait(false);

        if (!await read.ReadAsync().ConfigureAwait(false)) return Results.StatusCode(StatusCodes.Status404NotFound);

        var saldo_cliente = read.GetInt32(0);
        var limite_cliente = read.GetInt32(1);
        var data_extrato = DateTime.UtcNow;
        var ultimas_transacoes = new List<TransacaoExtrato>();
        read.Close();

        query = "SELECT valor, tipo, descricao, realizado_em FROM Transacao WHERE cliente_id = @cliente_id ORDER BY realizado_em DESC LIMIT 10";
        command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("cliente_id", id);
        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            ultimas_transacoes.Add(new TransacaoExtrato
            {
                valor = reader.GetInt32(0),
                tipo = reader.GetChar(1),
                descricao = reader.GetString(2),
                realizada_em = reader.GetDateTime(3)
            });
        }

        return Results.Ok(new ExtratoResponse
        {
            saldo = new SaldoExtrato(saldo_cliente, data_extrato, limite_cliente),
            ultimas_transacoes = ultimas_transacoes
        });
    }
});

string? port = Environment.GetEnvironmentVariable("PORT");
if (port is null) throw new ArgumentNullException("PORT is null");

await app.RunAsync("http://localhost:" + port);

public enum CriarTransacaoRetorno {
    NotFound = 1,
    LimitExceeded = 2,
}
public record struct TransacaoPayload(float Valor, char Tipo, string Descricao);
public record struct TransacaoResponse(int saldo, int limite);
public record struct TransacaoExtrato(int valor, char tipo, string descricao, DateTime realizada_em);
public record struct SaldoExtrato(int total, DateTime data_extrato, int limite);
public record struct ExtratoResponse(SaldoExtrato saldo, List<TransacaoExtrato> ultimas_transacoes);

[JsonSerializable(typeof(TransacaoPayload))]
[JsonSerializable(typeof(TransacaoResponse))]
[JsonSerializable(typeof(TransacaoExtrato))]
[JsonSerializable(typeof(SaldoExtrato))]
[JsonSerializable(typeof(ExtratoResponse))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(DateTime))]
public partial class JsonContext : JsonSerializerContext { }
