# EF Core & Migrations

> [!NOTE]
> As migrações do banco de dados são gerenciadas através do **Entity Framework Core 10** e aplicadas de acordo com o ambiente de execução (Desenvolvimento vs Produção).

---

## 🛠️ Comandos Úteis do EF Core CLI

### Criar uma nova migration
Ao alterar qualquer entidade na pasta `Domain/` ou configurações no `AppDbContext.cs`:

```powershell
dotnet ef migrations add NomeDaMigration --project MudBlazorWebApp1 --startup-project MudBlazorWebApp1
```

### Atualizar o banco de dados localmente
```powershell
dotnet ef database update --project MudBlazorWebApp1 --startup-project MudBlazorWebApp1
```

---

## 🚀 Estratégia de Migrações por Ambiente

### Ambiente de Desenvolvimento
Em modo `Development`, a aplicação executa o `database.Migrate()` automaticamente durante o startup no `Program.cs`, facilitando o onboarding de novos desenvolvedores.

### Ambiente de Produção
Em `Production`, a flag `Database__ApplyMigrations` deve ser mantida como `false`. As migrações devem ser executadas exclusivamente por uma pipeline de CI/CD ou job isolado de deploy:

```powershell
dotnet ef database update --connection "Host=prod-db;Database=braseller;Username=...;Password=..."
```

---

## 🔗 Links Relacionados

- [[05 - 🗄️ Banco de Dados & Infraestrutura/Modelo de Dados PostgreSQL|Modelo PostgreSQL]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Setup Local]]

#efcore #migrations #dotnet #postgresql #cicd
