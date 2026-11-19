# API pública v1 — consultas

O recurso vem desativado. Use um tenant de homologação. Configure PublicApi__Enabled=true e PublicApi__Keys__<id>__Enabled=true, TenantId, ExpiresAt (ISO 8601 em UTC), Scopes (separados por espaço) e Sha256 (hex do hash do segredo). Gere pelo menos 32 bytes aleatórios e codifique em base64url sem padding para formar o segredo. Guarde o segredo com o integrador. O servidor recebe apenas o hash na configuração.

Envie `X-Api-Key: <id>.<segredo>`. Rotacione criando um novo ID, distribuindo a nova chave e desabilitando o ID antigo. A configuração é administrada pelo deploy, não pelo painel.

| Consulta | Escopo |
| --- | --- |
| GET /v1/tenant | tenant:read |
| GET /v1/contacts | contacts:read |
| GET /v1/conversations | conversations:read |
| GET /v1/conversations/{id}/messages | messages:read |
| GET /v1/usage | usage:read |

Swagger: /swagger/public-v1/swagger.json. As listas aceitam offset e limit. Uso é o mês corrente em UTC e reflete apenas registros de IA existentes, sem cobrança Meta.

```bash
curl --fail-with-body -H "X-Api-Key: $OMNI_API_KEY" "$OMNI_API_URL/v1/contacts?limit=20"
```

Este incremento não realiza envios. A liberação externa depende de testes de isolamento, limites por chave e homologação do ambiente.
