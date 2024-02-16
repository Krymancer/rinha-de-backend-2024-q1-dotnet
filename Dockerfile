FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
RUN apt-get update && apt-get install -y clang zlib1g-dev
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -r linux-amd64 -c Release -o out
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["./rinha"]
