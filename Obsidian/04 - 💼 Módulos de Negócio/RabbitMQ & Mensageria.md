# RabbitMQ & Mensageria Assíncrona

> [!IMPORTANT]
> O **RabbitMQ** foi integrado ao **BraSeller** como Broker de Mensagens de **baixa latência** e alta disponibilidade, responsável por gerenciar filas de trabalho, orquestração de tarefas assíncronas, envio de e-mails e consumo desacoplado de pedidos com confirmação explícita (`BasicAck`).

---

## 🏗️ Topologia da Arquitetura RabbitMQ

```mermaid
graph TD
    API[ASP.NET Core Web API / Connectors] -->|Publish JSON Payload| Exchange[Exchange: braseller.events - Topic]
    
    Exchange -->|Routing Key: order.#| QueueOrders[Fila: orders.queue]
    Exchange -->|Routing Key: email.#| QueueEmails[Fila: emails.queue]
    Exchange -->|Routing Key: task.#| QueueTasks[Fila: tasks.queue]
    
    QueueOrders -->|BasicAck ao concluir| WorkerOrders[OrderProcessingConsumerWorker]
    QueueEmails -->|BasicAck ao concluir| WorkerEmails[EmailConsumerWorker]
    QueueTasks -->|BasicAck ao concluir| WorkerTasks[AsyncTaskConsumerWorker]
    
    WorkerOrders -->|Nack (Falha)| DLX[Dead Letter Exchange: braseller.dlx]
    WorkerEmails -->|Nack (Falha)| DLX
    WorkerTasks -->|Nack (Falha)| DLX
    DLX --> DLQ[Fila: dead-letter.queue]
```

---

## 📋 Especificações das Filas & Workers

| Fila | Routing Key Pattern | Worker Responsável | Descrição do Trabalho |
|---|---|---|---|
| **`orders.queue`** | `order.#` | `OrderProcessingConsumerWorker` | Processamento desacoplado de pedidos importados dos conectores (Mercado Livre, Shopee). |
| **`emails.queue`** | `email.#` | `EmailConsumerWorker` | Despacho assíncrono e de alta vazão de e-mails da Outbox via `SmtpEmailSender`. |
| **`tasks.queue`** | `task.#` | `AsyncTaskConsumerWorker` | Orquestração de tarefas assíncronas pesadas (auditoria de fechamento contábil, relatórios DRE). |
| **`dead-letter.queue`** | `dead-letter` | N/A (Inspeção Manual) | Retém mensagens que falharam após tentativas de re-processamento para auditoria. |

---

## 🔒 Confirmação Explícita & Remoção de Mensagem (Ack / Nack)

Para garantir **zero perda de mensagens** e evitar processamento duplicado:

1. **`autoAck: false`**: Os trabalhadores não confirmam a recepção automaticamente.
2. **`BasicAck(deliveryTag)`**: Ao finalizar o processamento com sucesso, o consumidor envia a instrução `BasicAck`, o que **remove definitivamente a mensagem da fila** (Low Latency / Clean Queue).
3. **`BasicNack(requeue: false)`**: Em caso de exceção não tratada, o consumidor rejeita a mensagem sem colocar de volta na fila principal, direcionando-a para a **Dead Letter Queue (`dead-letter.queue`)**.

---

## 🛠️ Credenciais & Painel de Gerenciamento

O RabbitMQ vem containerizado via Docker Compose (`rabbitmq:4-management-alpine`).

- **Protocolo AMQP**: `localhost:5672`
- **Painel de Controle Web**: `http://localhost:15672`
- **Usuário Padrão**: `braseller`
- **Senha Padrão**: `braseller_secret`

---

## 🔗 Links Relacionados

- [[04 - 💼 Módulos de Negócio/Notificações & Outbox Pattern|Outbox Pattern]]
- [[05 - 🗄️ Banco de Dados & Infraestrutura/Docker, Terraform & Deploy|Docker & Infraestrutura]]
- [[06 - 🛠️ Guia de Desenvolvimento & API/Setup Local & Variáveis de Ambiente|Setup Local]]

#rabbitmq #messaging #async #work-queues #dead-letter #dotnet10
