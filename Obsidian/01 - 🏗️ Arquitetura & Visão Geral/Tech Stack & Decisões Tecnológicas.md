# Tech Stack & Decisões Tecnológicas

> [!NOTE]
> A escolha das tecnologias do **BraSeller** visa máxima performance, facilidade de manutenção, conformidade com os mais altos padrões de segurança e prontidão para nuvem.

---

## 🛠️ Matriz de Tecnologias

| Camada | Tecnologia / Biblioteca | Motivação & Decisão |
|---|---|---|
| **Runtime & Framework** | .NET 10 | Recursos modernos de C# 13, compilação AOT/Performance aprimorada, suporte LTS prolongado. |
| **Frontend UI** | Blazor WebAssembly + MudBlazor | SPA C# unificado, rica biblioteca de componentes Material Design, eliminando dependência de JS frameworks pesados. |
| **Banco de Dados** | PostgreSQL 18 | Robusteza ACID, suporte nativo a JSONB, excelente suporte a Row Level Security (RLS) e ordenação eficiente. |
| **ORM & Data Layer** | Entity Framework Core 10 | LINQ nativo, Global Query Filters para Multi-Tenant, Migrations robustas e Change Tracking. |
| **Autenticação** | ASP.NET Core Identity + JWT | JWT HMAC-SHA256 para requisições stateless de 10 min + Refresh Tokens de 512 bits renováveis armazenados com hash SHA-256 no banco. |
| **Geração de PDF** | QuestPDF (Community) | Engine fluente baseada em SkiaSharp para layouts altamente precisos de relatórios contábeis e financeiros. |
| **Compressão HTTP** | Brotli & Gzip | Compactação acelerada no Kestrel para tráfego otimizado de DLLs `.wasm` e APIs JSON. |
| **Containerização** | Docker & Docker Compose | Ambientes reprodutíveis com containers otimizados Alpine/Debian slim. |
| **Infraestrutura como Código** | Terraform | Gerenciamento declarativo da infraestrutura e recursos de nuvem. |

---

## 📋 Decisões Críticas de Projeto (ADRs)

### 1. Cookies `HttpOnly` com SameSite=Strict para Refresh Tokens
- **Contexto**: Armazenar Refresh Tokens no `localStorage` do navegador expõe a aplicação a ataques XSS.
- **Decisão**: O Refresh Token é trafegado exclusivamente via Cookie Seguro (`HttpOnly`, `Secure`, `SameSite=Strict`), inacessível por scripts client-side.

### 2. Filtro de Inquilino Obrigatório (`ITenantEntity`)
- **Contexto**: Risco de vazamento de dados entre concorrentes em ambiente SaaS Multi-Tenant.
- **Decisão**: Toda entidade de negócio implementa `ITenantEntity`. O EF Core injeta dinamicamente o `tenant_id` obtido do JWT token, tornando impossível para o código da aplicação esquecer o filtro `WHERE tenant_id = @tenant_id`.

### 3. Outbox Pattern Persistente por Tenant
- **Contexto**: Envio direto de e-mails em endpoints HTTP gera gargalos e perda de notificações em caso de instabilidade SMTP.
- **Decisão**: Notificações e e-mails são gravados na tabela `user_notifications` dentro da transação do tenant e processados em background por um `HostedService` (`NotificationWorker`).

---

## 🔗 Links Relacionados

- [[01 - 🏗️ Arquitetura & Visão Geral/Visao Geral do Sistema|Visão Geral do Sistema]]
- [[02 - 🔒 Segurança & Multi-Tenancy/Multi-Tenant Model & Query Filters|Isolamento Multi-Tenant]]
- [[05 - 🗄️ Banco de Dados & Infraestrutura/Docker, Terraform & Deploy|Docker & Terraform]]

#techstack #dotnet10 #postgresql #blazor #architecture-decisions
