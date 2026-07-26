# ADR 0001 — Monólito modular como arquitetura-base

- Status: aceito
- Data: 2026-07-24

## Contexto

O produto reúne autenticação multi-tenant, finanças, contabilidade, estoque,
fiscal, reconciliação, relatórios, notificações e conectores. A implantação ainda
é única, enquanto tarefas assíncronas já usam RabbitMQ.

## Decisão

Usar monólito modular com fatias verticais, regra de dependência da Clean
Architecture e padrão Web-Queue-Worker para cargas assíncronas. O host é a raiz
de composição. Novos microsserviços exigem evidência operacional e uma fronteira
de dados explícita.

## Consequências

- Uma implantação e transações locais continuam simples.
- Módulos ganham coesão e podem ser extraídos gradualmente.
- Escala horizontal replica o host completo no primeiro estágio.
- Limites internos precisam ser protegidos por revisão e testes arquiteturais.
- Filas exigem consumidores idempotentes, retries limitados e dead-letter.

## Alternativas rejeitadas

- **Tudo-em-um sem limites:** barato no curto prazo, mas inadequado ao número de
  domínios e integrações.
- **N camadas tradicional:** organiza responsabilidades técnicas, porém espalha
  cada capacidade de negócio e faz o núcleo depender da persistência.
- **Microsserviços imediatos:** não há métricas que justifiquem complexidade de
  rede, consistência eventual e operação distribuída.
