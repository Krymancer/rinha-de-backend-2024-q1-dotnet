# Rinha Backend 2024 Q1

Essa é a minha implementação da [Rinha de Backend 2024 Q1](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/), realizada pelo [Zanfranceschi](https://twitter.com/zanfranceschi).

Você pode ver todas as instruções e a spec da API [aqui](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/blob/main/INSTRUCOES.md).

## Tecnologias utilizadas

- [Dotnet](https://dotnet.microsoft.com/)
- [Postgres](https://www.postgresql.org/)
- [Docker](https://www.docker.com/) e [Docker Compose](https://docs.docker.com/compose/)
- [Nginx](https://nginx.org)
- [Github Actions](https://docs.github.com/pt/actions/learn-github-actions/understanding-github-actions)
- [Gatling](https://gatling.io/)

## Disclaimer

A ideia aqui é utilizar a [Minimal API](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview?view=aspnetcore-8.0) e ver o quão enxuto eu consigo deixar o codigo. A ideia é que a aplicação seja o mais simples possível, sem muitas camadas de abstração e com alguns truques para tirar o que eu conseguir de performance.

## Infraestrutura

Toda a aplicação está dividida em diferentes containers como você pode ver no arquivo [docker-compose.yml](./docker-compose.yml). A aplicação tem 4 serviços:

- `api1` e `api2` que são duas imagens da API que rodam em paralelo
- `nginx` que é o proxy reverso que faz o balanceamento de carga entre as duas APIs
- `db` que é o banco de dados Postgres

## Aviso

⚠️ O código deste repositório não deve ser utilizado como referência para ambientes de produção. Algumas práticas foram aplicadas especificamente em prol da competição, e podem não ser saudáveis para sua aplicação.

## Minhas Redes

- [Twitter](https://twitter.com/joaodocodigo)
- [Linkedin](https://www.linkedin.com/in/junior-nascm)
