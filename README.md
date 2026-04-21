# Code DB Connections Metric Collector

API REST em .NET 8 que analisa o código-fonte de um repositório remoto e conta quantas conexões com bancos de dados estão presentes, independente do banco ou linguagem.

## Funcionamento

1. Recebe a URL de um repositório (GitHub, GitLab ou Bitbucket).
2. Baixa o arquivo ZIP do branch `main`.
3. Extrai e percorre todos os arquivos de código-fonte.
4. Aplica expressões regulares para identificar padrões de abertura de conexão com bancos de dados.
5. Retorna a contagem total como uma métrica.

### Linguagens e drivers suportados

| Linguagem | Bibliotecas / Drivers |
|---|---|
| C# / .NET | `SqlConnection`, `NpgsqlConnection`, `MySqlConnection`, `OracleConnection`, `SQLiteConnection`, `OdbcConnection`, `OleDbConnection`, `Db2Connection`, `DbContext` (EF Core) |
| Java | `DriverManager.getConnection`, `DataSource.getConnection`, `new *DataSource()` |
| Python | `create_engine` (SQLAlchemy), `psycopg2.connect`, `pymysql.connect`, `sqlite3.connect`, `pyodbc.connect`, `cx_Oracle.connect`, `mysql.connector.connect` |
| JavaScript / TypeScript | `mysql.createConnection`, `mysql.createPool`, `mongoose.connect`, `new Sequelize()`, `knex({...})` |
| PHP | `new PDO()`, `new mysqli()`, `mysqli_connect()` |
| Go | `sql.Open`, `sqlx.Open`, `gorm.Open` |
| Ruby | `ActiveRecord::Base.establish_connection`, `PG.connect`, `Mysql2::Client.new` |

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para rodar localmente)
- [Docker](https://www.docker.com/) e [Docker Compose](https://docs.docker.com/compose/) (para rodar em container)

## Executando localmente

```bash
cd src/CodeDbConnectionsMetricCollector.Api
dotnet run
```

A API ficará disponível em `http://localhost:5000`.

## Executando com Docker Compose

```bash
docker compose up --build
```

A API ficará disponível em `http://localhost:8080`.

## Endpoint

### `POST /collect`

Analisa um repositório e retorna a contagem de conexões com banco de dados.

**Request body:**

```json
{
  "repositoryUrl": "https://github.com/owner/repo"
}
```

**Response:**

```json
{
  "metric": {
    "name": "Number of Database Connections",
    "collectorStrategy": "source code"
  },
  "measurement": {
    "apiIdentifier": "https://github.com/owner/repo",
    "value": 5,
    "unit": "operations",
    "timestamp": "2026-04-21T13:12:27.459031647Z"
  }
}
```

**Exemplo com `curl`:**

```bash
curl -X POST http://localhost:8080/collect \
  -H "Content-Type: application/json" \
  -d '{"repositoryUrl": "https://github.com/owner/repo"}'
```

## Estrutura do projeto

```
.
├── docker-compose.yml
└── src/
    └── CodeDbConnectionsMetricCollector.Api/
        ├── Controllers/
        │   └── CollectController.cs
        ├── Models/
        │   ├── CollectRequest.cs
        │   └── CollectResponse.cs
        ├── Services/
        │   └── RepositoryAnalyzerService.cs
        ├── Dockerfile
        └── Program.cs
```
