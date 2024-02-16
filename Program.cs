using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;

string connectionString = "Host=db;Port=5432;Database=db;Username=user;Password=password;";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseExceptionHandler(c => c.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    
    if (exception is BadHttpRequestException badHttpRequestException)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await context.Response.CompleteAsync();
    }
}));

app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoPayload request) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    if (
        request is null || 
        request.Tipo != 'd' && request.Tipo != 'c' || 
        request.Valor <= 0 || 
        request.Descricao is null || request.Descricao.Length < 1 || request.Descricao.Length > 10
        )
    {
        return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
    }

    using (var connection = new Npgsql.NpgsqlConnection(connectionString))
    {
        connection.Open();

        var command = new Npgsql.NpgsqlCommand("realizar_transacao", connection);
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("t_client_id", id);
        command.Parameters.AddWithValue("t_valor", request.Tipo == 'd' ? -request.Valor : request.Valor);
        command.Parameters.AddWithValue("t_tipo", request.Tipo);
        command.Parameters.AddWithValue("t_descricao", request.Descricao);

        command.Parameters.Add(new Npgsql.NpgsqlParameter("o_saldo", NpgsqlTypes.NpgsqlDbType.Integer));
        command.Parameters["o_saldo"].Direction = System.Data.ParameterDirection.Output;

        command.Parameters.Add(new Npgsql.NpgsqlParameter("o_limite", NpgsqlTypes.NpgsqlDbType.Integer));
        command.Parameters["o_limite"].Direction = System.Data.ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        int saldo = command.Parameters["o_saldo"].Value != DBNull.Value ? Convert.ToInt32(command.Parameters["o_saldo"].Value) : -1;
        int limite = command.Parameters["o_limite"].Value != DBNull.Value ? Convert.ToInt32(command.Parameters["o_limite"].Value) : -1;

        if(saldo == -1 || limite == -1) return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);

        await connection.CloseAsync();

        return Results.Ok(new
        {
            Limite = limite,
            Saldo = saldo,
        });
    }
});

app.MapGet("/clientes/{id}/extrato", async (int id) =>
{
    if (id < 1 || id > 5) return Results.StatusCode(StatusCodes.Status404NotFound);

    using (var connection = new Npgsql.NpgsqlConnection(connectionString))
    {
        connection.Open();

        string query = "SELECT saldo, limite FROM cliente WHERE id = @id";
        var command = new Npgsql.NpgsqlCommand(query, connection);
        var read = await command.ExecuteReaderAsync();

        if (!read.Read()) return Results.StatusCode(StatusCodes.Status404NotFound);

        var saldo = read.GetInt32(0);
        var limite = read.GetInt32(1);
        var data_extrato = DateTime.UtcNow;
        var ultimas_transacoes = new List<object>();
        read.Close();

        query = "SELECT valor, tipo, descricao, realizado_em FROM Transacao WHERE cliente_id = @cliente_id ORDER BY realizado_em DESC LIMIT 10";
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
    }
});

await app.RunAsync("http://0.0.0.0:99");
public record TransacaoPayload([Required]int Valor, [Required]char Tipo, [Required]string Descricao);