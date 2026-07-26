# Testes & Cobertura

> [!NOTE]
> A qualidade e estabilidade do **BraSeller** é mantida por uma suíte automatizada de testes no projeto `MudBlazorWebApp1.Tests`.

---

## 🧪 Estrutura de Testes (`MudBlazorWebApp1.Tests`)

- **Testes Unitários**: Validação de calculadoras financeiras, regras de margem e parse de webhooks sem acesso a banco de dados.
- **Testes de Integração (`WebApplicationFactory`)**: Testam o fluxo completo de autenticação, rotação de tokens, filtros de tenant no banco PostgreSQL de testes e geração de relatórios PDF.

```powershell
# Executar todos os testes da solução
dotnet test MudBlazorWebApp1.slnx

# Executar exibindo o log detalhado no console
dotnet test MudBlazorWebApp1.slnx --logger "console;verbosity=detailed"
```

---

## 📊 Cobertura de Testes

Os testes cobrem cenários críticos como:
1. **Tentativa de vazamento Multi-Tenant**: Garante que o User A não consiga visualizar lançamentos do Tenant B mesmo passando IDs via parâmetro.
2. **Rotação e Reutilização de Refresh Token**: Valida a invalidação automática da família de tokens caso um token antigo seja reutilizado por um atacante.
3. **Imutabilidade de Período Fechado**: Tenta gravar despesas em meses encerrados e valida o lançamento de exceções.

---

## 🔗 Links Relacionados

- [[02 - 🔒 Segurança & Multi-Tenancy/Multi-Tenant Model & Query Filters|Segurança Multi-Tenant]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Setup Local]]

#testing #unit-tests #integration-tests #xunit #dotnet
