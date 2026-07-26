# Docker, Terraform & Deploy

> [!NOTE]
> Toda a infraestrutura local e remota do **BraSeller** é provisionada de forma declarativa e automatizada.

---

## 🐳 Docker Compose Local

O arquivo `docker-compose.yml` (ou infraestrutura Docker) inicializa os serviços necessários para rodar o ambiente completo localmente:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:18-alpine
    container_name: braseller_postgres
    environment:
      POSTGRES_DB: braseller_db
      POSTGRES_USER: braseller_user
      POSTGRES_PASSWORD: secret_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

### Inicialização em Background
```powershell
docker compose up -d --wait
```

---

## 🏗️ Terraform (Infrastructure as Code)

Localizado no diretório `infrastructure/docker/`, o código Terraform provisiona os recursos e volumes necessários:

```hcl
# infrastructure/docker/main.tf
terraform {
  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 3.0.0"
    }
  }
}
```

---

## 🔐 Configuração de Secrets em Produção

Em produção, os segredos **nunca** são salvos em arquivos `.env` ou no repositório Git. Devem ser injetados por variáveis de ambiente ou secret manager (ex: AWS Secrets Manager, Azure Key Vault, GCP Secret Manager):

```text
ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...
Jwt__Key=SUA_CHAVE_SUPER_SECRETA_COM_PELO_MENOS_256_BITS
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
```

---

## 🔗 Links Relacionados

- [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Setup Local]]
- [[05 - 🗄️ Banco de Dados & Infraestrutura/EF Core & Migrations|EF Core Migrations]]

#docker #terraform #deploy #infrastructure #devops
