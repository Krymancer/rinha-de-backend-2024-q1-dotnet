CREATE TABLE cliente (
    id INT PRIMARY KEY,
    saldo INT NOT NULL,
    limite INT NOT NULL
);

CREATE TABLE transacao (
    id SERIAL PRIMARY KEY,
    valor INT NOT NULL,
    tipo CHAR(1) NOT NULL,
    descricao VARCHAR(10) NOT NULL,
    realizado_em TIMESTAMP NOT NULL DEFAULT NOW(),
    cliente_id INT NOT NULL,
    FOREIGN KEY (cliente_id) REFERENCES cliente(id)
);

CREATE INDEX idx_cliente ON cliente(id) INCLUDE (saldo, limite);
CREATE INDEX idx_transacao_cliente ON transacao(cliente_id);

CREATE PROCEDURE realizar_transacao(
    t_client_id INT,
    t_valor INT,
    t_tipo CHAR(1),
    t_descricao VARCHAR(10),
    INOUT o_saldo INT DEFAULT NULL,
    INOUT o_limite INT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    WITH saldo_atualizado AS (
        UPDATE cliente
        SET saldo = saldo + t_valor
        WHERE id = t_client_id AND saldo + t_valor >= -limite
        RETURNING saldo, limite
    ), inserido AS (
        INSERT INTO transacao (valor, tipo, descricao, cliente_id)
        SELECT ABS(t_valor), t_tipo, t_descricao, t_client_id
        FROM saldo_atualizado
    )
    SELECT saldo, limite
    INTO o_saldo, o_limite
    FROM saldo_atualizado;
END;
$$;

INSERT INTO cliente (Id, limite, saldo) VALUES
(1, 100000, 0),
(2, 80000, 0),
(3, 1000000, 0),
(4, 10000000, 0),
(5, 500000, 0);
