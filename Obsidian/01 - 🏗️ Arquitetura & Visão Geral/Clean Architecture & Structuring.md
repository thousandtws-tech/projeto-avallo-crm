# Clean Architecture & Feature Folders

> [!TIP]
> O projeto adota uma combinação poderosa de **Clean Architecture** para a separação de responsabilidades centrais (Domain, Infrastructure) e **Feature Folders (Vertical Slices)** para os módulos de negócio.

---

## 📂 Estrutura de Diretórios

### `MudBlazorWebApp1` (Backend & Host)

```text
MudBlazorWebApp1/
├── Domain/                         # Entidades de domínio puras, interfaces centrais e enums
│   ├── Tenant.cs                   # Raiz de agregação Multi-Tenant
│   ├── ApplicationUser.cs          # Usuário ASP.NET Core Identity
│   ├── FinancialEntry.cs           # Lançamento financeiro
│   ├── Accounting.cs               # Entidades de contabilidade
│   ├── Expense.cs                  # Despesas
│   └── UserNotification.cs         # Entidade Outbox de notificação
├── Infrastructure/                 # Implementações de infraestrutura e persistência
│   ├── MultiTenancy/               # Resolução e contextos de Tenant
│   ├── Persistence/                # ApplicationDbContext, EF Core Configurations, Migrations
│   ├── ExternalServices/           # Integração com serviços de e-mail, S3/Object Storage
│   └── Storage/                    # Armazenamento seguro de tokens e credenciais
├── Features/                       # Slices verticais agrupados por funcionalidade
│   ├── Auth/                       # Controllers / Endpoints de Autenticação e Gestão de Usuários
│   ├── Connectors/                 # Gerenciamento de plugins de marketplaces
│   ├── Reconciliation/             # Motores de conciliação financeira
│   ├── PeriodClosing/              # Encerramento de período contábil
│   ├── Reports/                    # Gerador de relatórios (QuestPDF, Excel, CSV)
│   └── Notifications/              # Outbox Worker & Dispatcher
└── connectors/                     # Diretório de carregamento dinâmico de assemblies de conectores
```

---

## 🔁 Fluxo de Dependências

```mermaid
graph LR
    Domain[Domain Core] <-- Infrastructure[Infrastructure Layer]
    Domain <-- Features[Features / API Layer]
    Infrastructure <-- Features
    Abstractions[BraSeller.Connectors.Abstractions] <-- Domain
    Abstractions <-- Connectors[Marketplace Plugins]
```

- **Domain**: Não conhece banco de dados, HTTP ou frameworks visuais.
- **BraSeller.Connectors.Abstractions**: Totalmente independente; apenas abstrações de conectores.
- **Features**: Consomem serviços registrados no contêiner de Injeção de Dependência (`IServiceCollection`).

---

## 🔗 Links Relacionados

- [[01 - 🏗️ Arquitetura & Visão Geral/Visao Geral do Sistema|Visão Geral do Sistema]]
- [[03 - 🔌 SDK & Conectores Marketplace/SDK de Conectores (Abstractions)|SDK de Conectores]]
- [[05 - 🗄️ Banco de Dados & Infraestrutura/Modelo de Dados PostgreSQL|Modelo PostgreSQL]]

#arquitetura #clean-architecture #dotnet #design-patterns
