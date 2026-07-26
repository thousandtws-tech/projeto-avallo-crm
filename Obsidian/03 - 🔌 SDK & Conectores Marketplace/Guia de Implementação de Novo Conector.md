# Guia de Implementação de Novo Conector

> [!TIP]
> Graças à arquitetura baseada no SDK `BraSeller.Connectors.Abstractions`, adicionar uma nova integração com **Shopee**, **Amazon**, **Magalu** ou **B2W** não exige nenhuma alteração no código fonte do Core do sistema.

---

## 📝 Passo a Passo para Criar um Conector (ex: Shopee)

### 1. Criar o Projeto de Biblioteca de Class (.NET 10)
Crie uma pasta sob o repositório chamada `BraSeller.Connector.Shopee` e adicione a referência apenas para `BraSeller.Connectors.Abstractions.csproj`:

```bash
dotnet new classlib -n BraSeller.Connector.Shopee -f net10.0
dotnet add BraSeller.Connector.Shopee reference BraSeller.Connectors.Abstractions/BraSeller.Connectors.Abstractions.csproj
```

### 2. Implementar a Interface `IConnectorModule`
Crie a classe de registro de serviços do módulo:

```csharp
namespace BraSeller.Connector.Shopee;

public sealed class ShopeeModule : IConnectorModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMarketplaceConnector, ShopeeConnector>();
        services.AddHttpClient<ShopeeConnector>();
    }
}
```

### 3. Implementar a Interface `IMarketplaceConnector`
Defina as credenciais necessárias no `Descriptor` e implemente os métodos de sincronização:

```csharp
public sealed class ShopeeConnector : IMarketplaceConnector
{
    public ConnectorDescriptor Descriptor => new(
        Name: "Shopee",
        DisplayName: "Shopee Marketplace",
        Version: "1.0.0",
        CredentialFields: [
            new ConnectorCredentialField("partner_id", "Partner ID", Required: true),
            new ConnectorCredentialField("partner_key", "Partner Key", Secret: true, Required: true)
        ],
        UsesOAuth: true
    );

    public async Task<ConnectorAuthentication> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken)
    {
        // Lógica de obtenção de token da Shopee API
    }

    // Implementar GetOrdersAsync, GetPaymentsAsync, GetFeesAsync e SyncAllAsync...
}
```

### 4. Compilar e Disponibilizar a DLL
Em ambiente de produção com carregamento dinâmico, compile o projeto e copie o assembly gerado para o diretório `connectors/`:

```bash
dotnet build BraSeller.Connector.Shopee -c Release -o ./connectors/Shopee
```

---

## 🔗 Links Relacionados

- [[03 - 🔌 SDK & Conectores Marketplace/SDK de Conectores (Abstractions)|SDK de Conectores]]
- [[03 - 🔌 SDK & Conectores Marketplace/Conector Mercado Livre|Exemplo Mercado Livre]]

#connectors #shopee #amazon #sdk #plugin-architecture
