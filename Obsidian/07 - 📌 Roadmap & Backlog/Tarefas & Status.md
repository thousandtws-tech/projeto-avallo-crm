# Tarefas & Status (Roadmap & Backlog)

> [!NOTE]
> Acompanhamento de entregas concluídas, funcionalidades em andamento e próximos marcos de desenvolvimento.

---

## 🟢 Concluído (Entregue)

- [x] **Arquitetura Base .NET 10 & PostgreSQL 18**: Configuração de EF Core, Migrations e isolamento de banco.
- [x] **Autenticação Multi-Tenant**: ASP.NET Core Identity, JWT HMAC-SHA256, Refresh Token em Cookie HttpOnly com rotação automática.
- [x] **SDK de Conectores Marketplace**: Criação de `BraSeller.Connectors.Abstractions` e contrato `IMarketplaceConnector`.
- [x] **Conector Mercado Livre**: Integração OAuth2, busca de pedidos, tarifas, repasses e Rate Limiter.
- [x] **Modulo de Notificações Outbox**: Worker assíncrono com retry exponencial e suporte a SMTP.
- [x] **Engine de Exportação de Relatórios**: Exportação em PDF (QuestPDF), Excel (Multi-sheet) e CSV (UTF-8 BOM).

---

## 🟡 Em Andamento

- [ ] **Integração com Web Push Notifications**: Notificação em tempo real no navegador para novas vendas e conciliações pendentes.
- [ ] **Painel de Dashboard Interativo MudBlazor**: Telas de gráficos de vendas diárias e margem de contribuição.
- [ ] **Certificados Digitais A1**: Suporte à emissão direta de NF-e / NFS-e.

---

## 🔵 Próximas Entregas (Backlog Priorizado)

- [ ] **Conector Shopee**: Implementação do plugin `BraSeller.Connector.Shopee`.
- [ ] **Conector Amazon**: Implementação da integração via Selling Partner API (SP-API).
- [ ] **Motor de Regras tributárias avançadas**: Suporte automático a Substituição Tributária (ST) e DIFAL entre estados.

---

## 🔗 Links Relacionados

- [[07 - 📌 Roadmap & Backlog/Ideias & Futuras Integrações|Ideias & Futuras Integrações]]
- [[00 - 🏠 Inicio|Voltar ao Início]]

#roadmap #backlog #tasks #status #sprint
