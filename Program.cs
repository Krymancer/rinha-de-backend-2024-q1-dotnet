string connectionString = "Host=db;Port=5432;Database=db;Username=user;Password=password;Maximum Pool Size=131072;";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request) =>
{
    if (request is null || request.Tipo != 'd' && request.Tipo != 'c' || request.Valor <= 0 || request.Descricao.Length < 1)
    {
        return Results.StatusCode(StatusCodes.Status400BadRequest);
    }

    var connection = new Npgsql.NpgsqlConnection(connectionString);
    connection.Open();

    string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
    var command = new Npgsql.NpgsqlCommand(query, connection);
    var read = await command.ExecuteReaderAsync();

    if (!read.Read()) return Results.StatusCode(StatusCodes.Status404NotFound);

    var saldo = read.GetInt32(0);
    var limite = read.GetInt32(1);

    if (request.Tipo == 'c') saldo += request.Valor;
    else if (request.Tipo == 'd' && saldo - request.Valor < -limite) return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    else saldo -= request.Valor;

    query = "INSERT INTO transacao (valor, tipo, descricao, realizado_em, cliente_id) VALUES (@valor, @tipo, @descricao, @realizado_em, @cliente_id)";
    command = new Npgsql.NpgsqlCommand(query, connection);
    command.Parameters.AddWithValue("valor", request.Valor);
    command.Parameters.AddWithValue("tipo", request.Tipo);
    command.Parameters.AddWithValue("descricao", request.Descricao);
    command.Parameters.AddWithValue("realizado_em", DateTime.UtcNow);
    command.Parameters.AddWithValue("cliente_id", id);

    await command.ExecuteNonQueryAsync();

    query = "UPDATE cliente SET saldo = @saldo WHERE id = @id";
    command = new Npgsql.NpgsqlCommand(query, connection);
    command.Parameters.AddWithValue("saldo", saldo);
    command.Parameters.AddWithValue("id", id);

    await command.ExecuteNonQueryAsync();

    await connection.CloseAsync();

    return Results.Ok(new
    {
        Limite = limite,
        Saldo = saldo,
    });
});

app.MapGet("/clientes/{id}/extrato", async (int id) =>
{
    var connection = new Npgsql.NpgsqlConnection(connectionString);
    connection.Open();

    string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
    var command = new Npgsql.NpgsqlCommand(query, connection);
    var read = await command.ExecuteReaderAsync();

    if (!read.Read()) return Results.StatusCode(StatusCodes.Status404NotFound);

    var saldo = read.GetInt32(0);
    var limite = read.GetInt32(1);
    var data_extrato = DateTime.UtcNow;
    var ultimas_transacoes = new List<object>();

    query = "SELECT valor, tipo, descricao, realizada_em FROM Transacao WHERE cliente_id = @cliente_id ORDER BY realizada_em DESC LIMIT 10";
    command = new Npgsql.NpgsqlCommand(query, connection);
    command.Parameters.AddWithValue("cliente_id", id);
    var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        ultimas_transacoes.Add(new
        {
            valor = reader.GetInt32(0),
            tipo = reader.GetChar(1),
            descricao = reader.GetString(2),
            realizada_em = reader.GetDateTime(3)
        });
    }

    await connection.CloseAsync();

    return Results.Ok(new
    {
        saldo = new
        {
            total = saldo,
            data_extrato = data_extrato,
            limite = limite,
        },
        ultimas_transacoes = ultimas_transacoes
    });
});

await app.RunAsync("http://0.0.0.0:80");
public record TransacaoPayload(int Valor, char Tipo, string Descricao);