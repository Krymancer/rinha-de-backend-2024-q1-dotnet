using Npgsql;

string connectionString = "Host=db;Port=5432;Database=db;Username=user;Password=password;Maximum Pool Size=1024;Timeout=1;";
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    if (
        request is null ||
        request.Tipo != 'd' && request.Tipo != 'c' ||
        request.Valor <= 0 || !(request.Valor % 1 == 0) ||
        request.Descricao is null || request.Descricao.Length < 1 || request.Descricao.Length > 10
        )
    {
        return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    }

    using (var connection = new NpgsqlConnection(connectionString))
    {
        connection.Open();

        string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
        var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("id", id);
        var read = await command.ExecuteReaderAsync();

        if (!read.Read()) return Results.StatusCode(StatusCodes.Status404NotFound);

        var saldo_cliente = read.GetInt32(0);
        var limite_cliente = read.GetInt32(1);
        read.Close();

        if (request.Tipo == 'c')
        {
            saldo_cliente += (int)request.Valor;
        }
        else if (saldo_cliente - request.Valor < limite_cliente * -1)
        {
            return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
        }
        else
        {
            saldo_cliente -= (int)request.Valor;
        }

        using (var transaction = connection.BeginTransaction())
        {
            query = "INSERT INTO Transacao (valor, tipo, descricao, cliente_id) VALUES (@valor, @tipo, @descricao, @cliente_id)";
            using (command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("valor", (int)request.Valor);
                command.Parameters.AddWithValue("tipo", request.Tipo);
                command.Parameters.AddWithValue("descricao", request.Descricao);
                command.Parameters.AddWithValue("cliente_id", id);
                await command.ExecuteNonQueryAsync();
            }

            query = "UPDATE cliente SET saldo = @saldo WHERE id = @id";
            using (command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("saldo", saldo_cliente);
                command.Parameters.AddWithValue("id", id);
                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }

        await connection.CloseAsync();

        return Results.Ok(new
        {
            Limite = limite_cliente,
            Saldo = saldo_cliente,
        });
    }
});

app.MapGet("/clientes/{id}/extrato", async (int id) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    using (var connection = new NpgsqlConnection(connectionString))
    {
        connection.Open();
        string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
        var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("id", id);
        var read = await command.ExecuteReaderAsync();

        if (!read.Read()) return Results.StatusCode(StatusCodes.Status404NotFound);

        var saldo_cliente = read.GetInt32(0);
        var limite_cliente = read.GetInt32(1);
        var data_extrato = DateTime.UtcNow;
        var ultimas_transacoes = new List<object>();
        read.Close();

        query = "SELECT valor, tipo, descricao, realizado_em FROM Transacao WHERE cliente_id = @cliente_id ORDER BY realizado_em DESC LIMIT 10";
        command = new NpgsqlCommand(query, connection);
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
                total = saldo_cliente,
                data_extrato = data_extrato,
                limite = limite_cliente,
            },
            ultimas_transacoes = ultimas_transacoes
        });
    }
});

await app.RunAsync("http://0.0.0.0:3000");
public record TransacaoPayload(float Valor, char Tipo, string Descricao);