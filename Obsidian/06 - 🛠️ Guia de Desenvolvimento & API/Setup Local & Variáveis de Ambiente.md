# Setup Local & Variáveis de Ambiente

> [!TIP]
> Guia passo a passo para configurar o ambiente de desenvolvimento local na sua máquina.

---

## 📋 Pré-requisitos

- **.NET 10 SDK** instilado.
- **Docker Desktop** ativo.
- **Git** instalado.
- IDE recomendada: **Visual Studio 2022 / Rider / VS Code**.

---

## 🚀 Passo a Passo de Execução

### 1. Clonar e Acessar o Repositório
```powershell
git clone <URL_DO_REPOSITORIO>
cd MudBlazorWebApp1
```

### 2. Configurar o Arquivo `.env`
Copie os valores do modelo `.env.example` para `.env`:
```powershell
Copy-Item .env.example .env
```

### 3. Subir os Containers do Banco de Dados
```powershell
docker compose up -d --wait
```

### 4. Compilar a Solução
```powershell
dotnet build MudBlazorWebApp1.slnx -c Debug
```

### 5. Executar a Aplicação Web
```powershell
dotnet run --project MudBlazorWebApp1
```

Acesse no navegador: `https://localhost:7001` ou `http://localhost:5000`.

---

## 🔑 Principais Variáveis de Ambiente (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=braseller;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Issuer": "BraSeller",
    "Audience": "BraSellerClient",
    "Key": "SUA_CHAVE_SUPER_SECRETA_E_LONGA_DE_PELO_MENOS_32_CARACTERES"
  },
  "Reports": {
    "QuestPdfLicense": "Community"
  }
}
```

---

## 🔗 Links Relacionados

- [[06 - 🛠️ Guia de Desenvolvimento & API/Catalogo de APIs|Catálogo de APIs]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Testes & Cobertura|Executar Testes]]

#setup #developer-guide #dotenv #dotnet10 #docker
