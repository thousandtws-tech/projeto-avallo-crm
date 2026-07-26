# MudBlazorWebApp1

Base de autenticacao multi-tenant em .NET 10 e PostgreSQL 18.

## Arquitetura

O BraSeller evolui como um monolito modular: fatias verticais por capacidade de
negocio, dependencias orientadas para o dominio e workers assincronos para cargas
longas ou sujeitas a picos. A comparacao das alternativas, regras de dependencia,
estrutura-alvo e gatilhos para extracao de servicos estao em
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). A decisao esta registrada no
[`ADR 0001`](docs/adr/0001-modular-monolith.md).

## Seguranca

- ASP.NET Core Identity para senha, lockout e papeis.
- JWT HMAC-SHA256 com validade de 10 minutos.
- Refresh token aleatorio de 512 bits, armazenado apenas como SHA-256 no banco.
- Rotacao de refresh token e deteccao de reutilizacao, com cookie `HttpOnly`, `Secure` e `SameSite=Strict`.
- Tenant obtido exclusivamente da claim assinada `tenant_id`; o cliente nunca escolhe o tenant de uma consulta.
- Filtro global para toda entidade que implementa `ITenantEntity` e bloqueio adicional de escrita no `SaveChanges`.
- `Admin` gerencia usuarios e revisoes contabeis, `Admin` e `Vendedor` podem escrever, `Contador` usa somente endpoints de leitura e download.

O cadastro inicial cria um tenant e seu usuario `Admin`. O endpoint administrativo de usuarios permite criar acessos `Vendedor` ou `Contador` dentro do mesmo tenant.

## Configuracao local

1. Inicie o Docker Desktop.
2. Copie os valores de `.env.example` para um `.env` local e troque a senha.
3. Execute `docker compose up -d --wait`.
4. Ajuste a connection string de desenvolvimento caso tenha trocado os valores do PostgreSQL.
5. Execute `dotnet run --project MudBlazorWebApp1`.

Em desenvolvimento, as migrations sao aplicadas automaticamente. Em producao, use um job de deploy com `dotnet ef database update` e mantenha `Database__ApplyMigrations=false`.

Segredos de producao devem ser definidos pelo provedor de secrets:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Authentication__Google__ClientId
Authentication__Google__ClientSecret
```

No Google Cloud Console, configure a URI autorizada de redirecionamento como `https://SEU_HOST/signin-google`.

## API

| Metodo | Rota | Acesso |
|---|---|---|
| POST | `/api/auth/register` | Publico |
| POST | `/api/auth/login` | Publico |
| POST | `/api/auth/refresh` | Cookie de refresh |
| POST | `/api/auth/logout` | Cookie de refresh |
| GET | `/api/auth/google?tenantName=...` | Publico |
| GET | `/api/auth/me` | Admin, Vendedor, Contador |
| POST | `/api/users` | Admin |

Para `/api/users`, informe `role` como `Vendedor` ou `Contador`. Endpoints de escrita de dados do negocio devem exigir a policy `CanWrite`; endpoints de leitura devem exigir `TenantMember`.

O administrador gerencia esses acessos em `/users`. Novos usuarios recebem uma senha temporaria e nao podem acessar os modulos do tenant antes de substitui-la no primeiro login. Ao desativar um acesso, JWTs sao invalidados pelo security stamp e refresh tokens ativos sao revogados.

## Validacao

```powershell
dotnet build MudBlazorWebApp1.slnx -c Release
dotnet test MudBlazorWebApp1.slnx
```

## Exportacao de relatorios

O endpoint `GET /api/reports/export` usa os mesmos filtros tenant-aware do painel e aceita:

- `format=pdf|xlsx|csv`
- `mode=consolidated|platform`
- `from`, `to`, `platform`, `paymentMethod` e `status`

O PDF inclui resumo mensal e layout para conferencia contabil. O Excel inclui uma aba consolidada e uma aba para cada marketplace. O CSV usa UTF-8 com BOM, separador `;` e valores monetarios em formato decimal invariavel.

O projeto configura `Reports:QuestPdfLicense` como `Community`. Antes de uso comercial, confirme a elegibilidade nos termos do QuestPDF ou altere essa configuracao para a licenca adquirida.

## Notificacoes e e-mail

A central usa uma outbox persistente por tenant. Fechamentos mensais, alertas de liberacao do Mercado Livre, novas vendas habilitadas pelo usuario e relatorios semanais do contador sao deduplicados e processados em background. Falhas SMTP usam retry exponencial e permanecem registradas.

Configure o SMTP por secrets ou variaveis de ambiente:

```text
Email__Enabled=true
Email__Host=smtp.seu-provedor.com
Email__Port=587
Email__Username=...
Email__Password=...
Email__FromEmail=notificacoes@seu-dominio.com
Email__FromName=Nucleo
Email__Security=StartTls
```

Valores aceitos para `Email__Security` incluem `StartTls`, `SslOnConnect` e `None`. Mantenha `StartTls` ou `SslOnConnect` em producao. Com o envio desabilitado, notificacoes e e-mails continuam na outbox e nao sao perdidos.

## SDK de conectores

`BraSeller.Connectors.Abstractions` e o unico projeto que um modulo de marketplace deve referenciar. O Core nao possui referencia para Mercado Livre, Shopee, Amazon ou qualquer implementacao concreta.

Um plugin deve implementar:

- `IMarketplaceConnector` com autenticacao, refresh, pedidos, detalhes, pagamentos, taxas, NF opcional, sincronizacao e status.
- `IConnectorModule` para registrar o conector e suas dependencias no DI.
- `ConnectorDescriptor.CredentialFields` para a interface montar o formulario sem conhecer a plataforma.

Publique o plugin, nomeado como `BraSeller.Connector.{Marketplace}.dll`, e suas dependencias na pasta configurada em `Connectors:PluginPath`, por padrao `MudBlazorWebApp1/connectors`. No proximo startup, o loader isolado descobre o modulo automaticamente. Nao adicione `ProjectReference` do plugin ao Core.

Todos os pedidos devem retornar `StandardOrder`. Na API, o contrato usa `order_id`, `platform`, `date`, `gross_value`, `platform_fee`, `net_value`, `payment_method`, `payment_date`, `release_date`, `status`, `buyer_name`, `items` e `invoice_number`. Status sao serializados como `paid`, `pending` ou `cancelled`.

Tokens sao criptografados com ASP.NET Core Data Protection usando finalidades separadas por tenant e conector. Em producao com mais de uma instancia, persista e compartilhe o key ring do Data Protection; perder essas chaves exige reconectar os marketplaces.

## Conciliacao financeira

O modulo em `/reconciliation` importa extratos OFX ou CSV de ate 5 MB. Para CSV, use cabecalhos equivalentes a `Data`, `Valor`, `Descricao` e, opcionalmente, `Id` ou `Referencia`. O formato monetario brasileiro e aceito.

O motor sugere pagamentos por referencia, valor e proximidade da data de liberacao. Admin e Contador podem confirmar um credito contra um ou varios pagamentos ou ignora-lo com justificativa. A confirmacao gera um lancamento contabil imutavel, debitando `Bancos (1.1.01)` e creditando `Valores a receber de marketplaces (1.1.02)`.

Arquivos repetidos sao bloqueados por SHA-256, os originais sao preservados no storage e creditos/repasses pendentes impedem o fechamento mensal. Transacoes de meses fechados so podem ser importadas depois da reabertura formal da competencia.

`syncAll` e idempotente por `tenant + platform + order_id`, salva pedido e itens normalizados e atualiza o lancamento financeiro que alimenta painel, relatorios, exportacoes e notificacoes.

## Conector Mercado Livre

O plugin `BraSeller.Connector.MercadoLivre` implementa OAuth 2.0, refresh de token, pedidos, detalhes, pagamentos, taxas, status e sincronizacao completa. Datas recebidas em UTC sao normalizadas para `America/Sao_Paulo`. O rate limiter aplica no maximo 100 requisicoes por minuto para cada token, respeitando o limite de 6.000 por hora.

Crie um aplicativo em `developers.mercadolivre.com.br` e configure exatamente esta redirect URI no ambiente local:

```text
https://localhost:7128/api/connectors/oauth/callback
```

Armazene as credenciais com user-secrets:

```powershell
dotnet user-secrets set "Connectors:MercadoLivre:ClientId" "SEU_APP_ID" --project "MudBlazorWebApp1/MudBlazorWebApp1.csproj"
dotnet user-secrets set "Connectors:MercadoLivre:ClientSecret" "SEU_CLIENT_SECRET" --project "MudBlazorWebApp1/MudBlazorWebApp1.csproj"
dotnet user-secrets set "Connectors:MercadoLivre:RedirectUri" "https://localhost:7128/api/connectors/oauth/callback" --project "MudBlazorWebApp1/MudBlazorWebApp1.csproj"
```

Depois, acesse `/connectors` e selecione **Conectar** no Mercado Livre. Em producao, troque a redirect URI para o dominio publico HTTPS e configure corretamente os forwarded headers do proxy.
