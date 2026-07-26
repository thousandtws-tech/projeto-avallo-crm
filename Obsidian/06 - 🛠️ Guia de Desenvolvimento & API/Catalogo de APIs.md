# Catálogo de APIs

> [!NOTE]
> Referência completa de todos os endpoints HTTP expostos pelo backend ASP.NET Core API.

---

## 🔑 Autenticação & Usuários (`/api/auth` e `/api/users`)

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/register` | *Público* | Registra um novo Tenant e seu usuário inicial `Admin`. |
| `POST` | `/api/auth/login` | *Público* | Autentica usuário com e-mail/senha. Retorna JWT + Cookie HttpOnly de Refresh. |
| `POST` | `/api/auth/refresh` | Cookie Refresh | Utiliza o Cookie de Refresh Token para emitir novo JWT sem relogar. |
| `POST` | `/api/auth/logout` | Cookie Refresh | Revoga o Refresh Token ativo no banco e limpa cookies. |
| `GET` | `/api/auth/google` | *Público* | Inicia o fluxo de Single Sign-On (SSO) com a conta do Google. |
| `GET` | `/api/auth/me` | `TenantMember` | Retorna o perfil do usuário autenticado e dados do Tenant. |
| `POST` | `/api/users` | RequireRole(`Admin`) | Permite ao Administrador criar usuários `Vendedor` ou `Contador` no Tenant. |

---

## 📊 Relatórios & Exportação (`/api/reports`)

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `GET` | `/api/reports/export` | `TenantMember` | Exporta relatório consolidado nos formatos `pdf`, `xlsx` ou `csv`. |

### Parâmetros da Query (`/api/reports/export`)
- `format`: `pdf` | `xlsx` | `csv` (Padrão: `pdf`)
- `mode`: `consolidated` | `platform` (Padrão: `consolidated`)
- `from`: Data inicial no formato ISO (`yyyy-MM-dd`)
- `to`: Data final no formato ISO (`yyyy-MM-dd`)
- `platform`: Filtro por marketplace (ex: `MercadoLivre`, `Shopee`)
- `status`: Filtro por status do lançamento (`Conciliado`, `Pendente`, `Divergência`)

---

## 🔌 Conectores Marketplace (`/api/connectors`)

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `GET` | `/api/connectors` | `TenantMember` | Lista todos os conectores disponíveis e status de conexão. |
| `POST` | `/api/connectors/{name}/connect` | `CanWrite` | Salva credenciais ou autoriza a integração do conector. |
| `POST` | `/api/connectors/{name}/sync` | `CanWrite` | Dispara sincronização manual de vendas e repasses. |

---

## 🔗 Links Relacionados

- [[02 - 🔒 Segurança & Multi-Tenancy/Roles, Policies & Permissões|Roles & Policies]]
- [[04 - 💼 Módulos de Negócio/Relatórios & Exportação (PDF, Excel, CSV)|Exportação de Relatórios]]

#api #rest #endpoints #swagger #postman
