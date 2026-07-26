# Módulo Fiscal

> [!NOTE]
> O **Módulo Fiscal** (`Fiscal`) gerencia os dados tributários do Tenant, perfis fiscais (Simples Nacional, Lucro Presumido), enquadramento CNAE e conferência de Notas Fiscais emitidas.

---

## 🏛️ Funcionalidades Fiscais

1. **Perfil Tributário (`TaxProfile`)**: Armazena Regime Tributário (Simples Nacional vs Lucro Presumido/Real), alíquotas médias estimadas e CNAEs primários/secundários.
2. **Regras Fiscais (`TaxRule`)**: Permite parametrizar alíquotas de tributação por categoria de produto ou estado de destino.
3. **Auditoria Tributária (`TaxAssessment`)**: Cruza o faturamento bruto das vendas importadas do marketplace com as Notas Fiscais emitidas para apontar omissões ou divergências de emissão antes da apuração mensal do DAS/impostos.

---

## 📄 Formatos de Nota Fiscal

O sistema processa notas em formato XML/JSON para:
- Atualizar automaticamente o Custo Médio de Aquisição no Estoque.
- Verificar o número da NF associada ao pedido importado do Mercado Livre (`invoice_number`).

---

## 🔗 Links Relacionados

- [[04 - 💼 Módulos de Negócio/Contabilidade & Fechamento de Período|Contabilidade]]
- [[04 - 💼 Módulos de Negócio/Relatórios & Exportação (PDF, Excel, CSV)|Exportação de Relatórios]]

#fiscal #nfe #simplesnacional #tributos #cnae
