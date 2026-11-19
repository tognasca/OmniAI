# Embedded Signup v4 em homologação

O modo de teste vem desativado. Use uma base de homologação e o sandbox de Embedded Signup do seu app Meta. Esse sandbox valida a autorização e os ativos. Ele é diferente do número de teste da Cloud API usado para enviar hello_world.

1. No app Meta existente, abra Facebook Login for Business e crie uma configuração de WhatsApp Embedded Signup para Cloud API. Autorize o domínio HTTPS do painel e a URI de retorno segundo a documentação Meta.
2. Em WhatsApp > Quickstart > Testing Integrations, solicite uma conta sandbox e confira os IDs de conta e número. Use a conta Meta administradora do app ao abrir o fluxo.
3. Configure no servidor `WhatsApp__SignupPreview__Enabled=true`, `WhatsApp__SignupPreview__TenantId`, `WhatsApp__SignupPreview__AppId`, `WhatsApp__SignupPreview__ConfigId`, `WhatsApp__SignupPreview__SandboxWabaId` e `WhatsApp__SignupPreview__SandboxPhoneId`. Mantenha `WhatsApp__AppSecret` no servidor. Não registre a URI de saída da troca OAuth, pois o contrato da Meta inclui credenciais na query.
4. Entre no painel com um administrador do tenant de homologação, abra Números e clique em Testar conexão com a Meta. Conclua pelo botão Finalizar da Meta para que o SDK entregue o código.
5. Confira que o painel mostra Sandbox validado. Teste cancelamento, tentativa em outro tenant e código expirado. Mantenha o número pessoal/operacional fora deste fluxo de teste.

Este piloto descarta o token após validar os ativos. O cadastro de números reais, a persistência cifrada e as operações de registro, assinatura de webhooks e monitoramento dependem da conclusão da fundação e de homologação com a Meta. A validação do sandbox não ativa cobrança.

[Documentação Meta](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/implementation)
