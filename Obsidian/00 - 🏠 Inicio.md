# 🧠 Cérebro do Projeto - MudBlazorWebApp1 (BraSeller)

> [!IMPORTANT]
> **BraSeller** é uma plataforma e-commerce SaaS multi-tenant desenvolvida em **.NET 10**, **Blazor WebAssembly (MudBlazor)** e **PostgreSQL 18**, projetada para a gestão integrada, conciliação financeira e contábil de vendedores em marketplaces no Brasil.

---

## 🗺️ Mapa de Conteúdo (Map of Content - MOC)

Este cofre (Obsidian Vault) contém a documentação completa, arquitetura, decisões técnicas e guiamentos práticos do ecossistema **MudBlazorWebApp1**.

### 1. 🏗️ [[01 - 🏗️ Arquitetura & Visão Geral/Visao Geral do Sistema|Arquitetura & Visão Geral]]
- [[01 - 🏗️ Arquitetura & Visão Geral/Visao Geral do Sistema|Visão Geral do Sistema]]: Propósito do negócio, stack e modelo mental.
- [[01 - 🏗️ Arquitetura & Visão Geral/Clean Architecture & Structuring|Clean Architecture & Feature Folders]]: Estrutura de projetos e organização modular.
- [[01 - 🏗️ Arquitetura & Visão Geral/Tech Stack & Decisões Tecnológicas|Tech Stack]]: Tecnologias (.NET 10, PostgreSQL 18, MudBlazor, QuestPDF, Docker, Terraform).

### 2. 🔒 [[02 - 🔒 Segurança & Multi-Tenancy/Multi-Tenant Model & Query Filters|Segurança & Multi-Tenancy]]
- [[02 - 🔒 Segurança & Multi-Tenancy/Multi-Tenant Model & Query Filters|Isolamento Multi-Tenant]]: `ITenantEntity`, EF Core Global Query Filters e segurança de banco.
- [[02 - 🔒 Segurança & Multi-Tenancy/Autenticação JWT & Refresh Tokens|Autenticação & JWT]]: Rotação de Refresh Token (512 bits), Cookies HttpOnly e Security Stamp.
- [[02 - 🔒 Segurança & Multi-Tenancy/Roles, Policies & Permissões|RBAC & Permissões]]: Papeis (`Admin`, `Vendedor`, `Contador`) e Policies ASP.NET Core.

### 3. 🔌 [[03 - 🔌 SDK & Conectores Marketplace/SDK de Conectores (Abstractions)|SDK & Conectores]]
- [[03 - 🔌 SDK & Conectores Marketplace/SDK de Conectores (Abstractions)|SDK de Conectores]]: Contratos desacoplados em `BraSeller.Connectors.Abstractions`.
- [[03 - 🔌 SDK & Conectores Marketplace/Conector Mercado Livre|Mercado Livre Plugin]]: Integração OAuth2, Rate Limiting, Pedidos, Taxas e Cobranças.
- [[03 - 🔌 SDK & Conectores Marketplace/Guia de Implementação de Novo Conector|Criar Novo Conector]]: Passo a passo para integrar Shopee, Amazon e Magalu.

### 4. 💼 [[04 - 💼 Módulos de Negócio/Reconciliação Financeira|Módulos de Negócio]]
- [[04 - 💼 Módulos de Negócio/Reconciliação Financeira|Reconciliação Financeira]]: Conciliação de repasses, tarifas de marketplace e fluxo de caixa.
- [[04 - 💼 Módulos de Negócio/Contabilidade & Fechamento de Período|Contabilidade & Fechamento]]: Fechamentos mensais, auditoria fiscal e travamento de período.
- [[04 - 💼 Módulos de Negócio/Gestão de Estoque|Gestão de Estoque]]: Controle de produtos, SKUs e sincronização multi-plataforma.
- [[04 - 💼 Módulos de Negócio/Módulo Fiscal|Módulo Fiscal]]: Integração com NF-e e notas de serviço.
- [[04 - 💼 Módulos de Negócio/Notificações & Outbox Pattern|Outbox & Notificações]]: Processamento assíncrono idempotente de e-mails/alertas com retry exponencial.
- [[04 - 💼 Módulos de Negócio/RabbitMQ & Mensageria|RabbitMQ & Mensageria]]: Filas de trabalho, troca de mensagens de baixa latência e consumo com Ack/Nack.
- [[04 - 💼 Módulos de Negócio/Relatórios & Exportação (PDF, Excel, CSV)|Relatórios & Exportações]]: PDF com QuestPDF, Excel multi-aba e CSV UTF-8 BOM.

### 5. 🗄️ [[05 - 🗄️ Banco de Dados & Infraestrutura/Modelo de Dados PostgreSQL|Banco de Dados & Infra]]
- [[05 - 🗄️ Banco de Dados & Infraestrutura/Modelo de Dados PostgreSQL|Modelo PostgreSQL 18]]: Entidades, índices e estratégia multi-inquilino.
- [[05 - 🗄️ Banco de Dados & Infraestrutura/EF Core & Migrations|EF Core & Migrations]]: Mapeamento, migrations automáticas em dev e jobs de CD em produção.
- [[05 - 🗄️ Banco de Dados & Infraestrutura/Docker, Terraform & Deploy|Docker & Terraform]]: Containerização local e infraestrutura como código (IaC).

### 6. 🛠️ [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Guia do Desenvolvedor]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Setup Local]]: Execução com Docker Desktop, `.env` e dotnet CLI.
- [[06 - 🛠️ Guia de Desenvolvimento & API/Catalogo de APIs|Catálogo de APIs]]: Endpoints HTTP, parâmetros, DTOs e matriz de autorização.
- [[06 - 🛠️ Guia de Desenvolvimento & API/Testes & Cobertura|Suíte de Testes]]: Testes unitários e de integração (`MudBlazorWebApp1.Tests`).

### 7. 📌 [[07 - 📌 Roadmap & Backlog/Tarefas & Status|Roadmap & Evolução]]
- [[07 - 📌 Roadmap & Backlog/Tarefas & Status|Backlog & Status]]: Tarefas em andamento e funcionalidades entregues.
- [[07 - 📌 Roadmap & Backlog/Ideias & Futuras Integrações|Futuras Integrações]]: Visão de produto, expansão de marketplaces e IA.

---

## ⚡ Atalhos Rápidos & Comandos

```powershell
# Executar a aplicação Web & API em desenvolvimento
dotnet run --project MudBlazorWebApp1

# Rodar a suíte de testes de integração e unitários
dotnet test MudBlazorWebApp1.slnx

# Compilar em modo Release
dotnet build MudBlazorWebApp1.slnx -c Release
```

---
#projeto #dotnet #blazor #multitenant #mercadolivre #postgresql #obsidian
