# WppConnect4Aspnet 📞

> WPPConnect é um projeto open source desenvolvido pela comunidade com o objetivo de exportar funções do WhatsApp Web através da lib 
> [wppconnect-team/wa-js](https://github.com/wppconnect-team/wa-js) para ser utilizado no Asp.NetCore#. Podem ser usadas para apoiar a criação de qualquer interação, como atendimento ao cliente, envio de mídia, reconhecimento de inteligência baseado em frases artificiais e muitas outras coisas. Use sua imaginação! 😀🤔💭



## Functions

|                                                            |    |
| ---------------------------------------------------------- |----|
| Atualização automática do QRCode                           | ✔ |
| Enviar **texto**        | ✔ |
| Enviar **imagem, vídeo, áudio e documentos**        | ❌ |
| Enviar stories **imagem, vídeo, texto**        | ✔  |
| Get **contacts, chats, groups, group members, Block List** |❌ |
| Enviar contatos                                            |❌ |
| Enviar stickers                                            |❌ |
| Enviar stickers GIF                                        |❌ |
| Múltiplas sessões                                          | ✔ |
| Encaminhar mensagens                                       |❌ |
| Receber mensagens                                          | ❌ |
| Enviar _localicação_                                       |❌ |
| **e muito mais**                                           | ✔ |


## Observações

> Esse projeto está sendo disponibilizado somente como BackEnd, o intuito é que possamos iplementar mais funções para futuramente incluirmos um front end aonde você desenvolvedor coloque novas funções no projeto.

## Como executar

> Instale o [Visual Studio 2022](https://visualstudio.microsoft.com/pt-br/vs/) ou superior com o workload de ASP.NET e desenvolvimento web.
> Instale o [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) ou superior.
> Clone este repositório.
> Abra o terminal na pasta do projeto e execute o comando `dotnet restore` para restaurar as dependências.
> Para que o banco de dados seja criado , execute o comando `dotnet ef migrations add InitialCreate`.
> A saída previsa no terminal será essa:
```
Build started...
Build succeeded.
Done. To undo this action, use 'dotnet ef migrations remove'
```
> Execute o comando `dotnet ef database update` para criar o banco de dados.
> A saída previsa no terminal será essa:
```
Build started...
Build succeeded.
Done.
```
> Execute o comando `dotnet run` para iniciar o servidor.
> Acesse `http://localhost:5000` no seu navegador para ver a aplicação em execução.

## Testando a API
> Inicie uma sessão enviando uma requisição POST para `http://localhost:5000/api/startSession` com o seguinte corpo JSON:
```json
  {
  "sessionId": "default"
  }
```
> Escaneie o QR Code com o WhatsApp do seu celular para conectar.
> Use ferramentas como Postman ou Insomnia para testar os endpoints da API.
> Para enviar mensagens, faça uma requisição POST para `http://localhost:5000/api/sendMessage` com o seguinte corpo JSON:
```json
  {
  "sessionId": "default",
  "to": "556299999999",
  "message": "Olá, esta é uma mensagem de teste!"
  }
```