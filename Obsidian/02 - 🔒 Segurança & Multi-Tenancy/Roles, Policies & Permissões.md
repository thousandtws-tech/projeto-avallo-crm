# Roles, Policies & Permissões

> [!NOTE]
> O **BraSeller** implementa um modelo de **Role-Based Access Control (RBAC)** refinado através de Policies declarativas no ASP.NET Core.

---

## 👥 Papéis do Sistema (Roles)

| Role | Descrição | Permissões de Leitura | Permissões de Escrita | Gestão de Usuários |
|---|---|---|---|---|
| **`Admin`** | Administrador do Tenant | ✅ Total | ✅ Total | ✅ Pode convocar, criar e desativar usuários no Tenant |
| **`Vendedor`** | Operador de Vendas / Financeiro | ✅ Total | ✅ Pode cadastrar despesas, lançamentos e conciliar | ❌ Sem acesso a gestão de usuários |
| **`Contador`** | Auditor Contábil / Fiscal | ✅ Acesso exclusivo de leitura e downloads (PDF, Excel, CSV) | ❌ Bloqueio estrito de escrita no banco | ❌ Sem acesso |

---

## 🛡️ Policies do ASP.NET Core

O sistema registra policies globais utilizadas nos Controllers e Endpoints:

### 1. `TenantMember` Policy
Exige que o usuário possua a claim `tenant_id` e pertença a qualquer uma das roles válidas (`Admin`, `Vendedor` ou `Contador`).

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("TenantMember", policy =>
        policy.RequireClaim("tenant_id")
              .RequireRole(Roles.Admin, Roles.Vendedor, Roles.Contador));
```

### 2. `CanWrite` Policy
Exige que o usuário seja **`Admin`** ou **`Vendedor`**. Perfis de **`Contador`** recebem `403 Forbidden` em rotas de mutação (`POST`, `PUT`, `DELETE`).

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanWrite", policy =>
        policy.RequireClaim("tenant_id")
              .RequireRole(Roles.Admin, Roles.Vendedor));
```

---

## 🔒 Matriz de Acesso aos Endpoints

| Endpoint | Método | Policy Necessária | Roles Permitidas |
|---|---|---|---|
| `/api/auth/register` | `POST` | *Público* | N/A |
| `/api/auth/login` | `POST` | *Público* | N/A |
| `/api/auth/refresh` | `POST` | Cookie Refresh | N/A |
| `/api/auth/me` | `GET` | `TenantMember` | `Admin`, `Vendedor`, `Contador` |
| `/api/users` | `POST` | RequireRole(`Admin`) | `Admin` |
| `/api/financial-entries` | `GET` | `TenantMember` | `Admin`, `Vendedor`, `Contador` |
| `/api/financial-entries` | `POST` | `CanWrite` | `Admin`, `Vendedor` |
| `/api/reports/export` | `GET` | `TenantMember` | `Admin`, `Vendedor`, `Contador` |

---

## 🔗 Links Relacionados

- [[02 - 🔒 Segurança & Multi-Tenancy/Autenticação JWT & Refresh Tokens|Autenticação JWT]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Catalogo de APIs|Catálogo de APIs]]

#security #rbac #roles #authorization #policies #aspnetcore
