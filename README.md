# ArcaPay - Plugin Rust

Plugin de entrega automatica de produtos para servidores Rust (Oxide/uMod).

## Requisitos

- Oxide/uMod instalado no servidor
- (Opcional) Plugin Economics para comandos de dinheiro

## Instalacao

1. Baixe `ArcaPay.cs` e coloque em `server/oxide/plugins/`
2. O servidor carrega automaticamente (ou use `oxide.reload ArcaPay`)
3. Edite `server/oxide/config/ArcaPay.json` com seu token
4. Use `/arcapay reload` para aplicar

## Configuracao

Edite `server/oxide/config/ArcaPay.json`:

```json
{
  "Token": "SEU_TOKEN_AQUI",
  "API URL": "https://arcapay.org/api/v1/fivem",
  "Poll Interval (seconds)": 10,
  "Identifier Type (steam/name)": "steam",
  "Debug": false
}
```

## Comandos no produto

Configure os comandos no painel ArcaPay.

### Comandos nativos do plugin

| Comando | Exemplo | Descricao |
|---------|---------|-----------|
| `addmoney` | `addmoney $steam 5000` | Adiciona saldo (Economics) |
| `giveitem` | `giveitem $steam rifle.ak 1` | Da item ao jogador |
| `addgroup` | `addgroup $steam vip` | Adiciona grupo Oxide |
| `grantperm` | `grantperm $steam kits.vip` | Concede permissao Oxide |

### Comandos de console (qualquer)

Qualquer comando que nao tenha handler especifico e executado no console:
```
inventory.giveto $steam rifle.ak 1
oxide.grant user $steam kits.vip
```

## Variaveis

Configure nas Variaveis da loja. O cliente preenche no checkout:
- `$steam` → Steam ID (ex: 76561198012345678)
- `$rust_name` → Nome no jogo

## Comandos in-game

| Comando | Descricao |
|---------|-----------|
| `/arcapay status` | Mostra status (apenas admin) |
| `/arcapay poll` | Forca polling manual |
| `/arcapay reload` | Recarrega configuracao |

## Plugins compativeis

- **Economics** - `addmoney` usa Economics automaticamente
- **ServerRewards** - use via console: `sr add $steam 100`
- **Kits** - use via console: `kit.give $steam vip_kit`
- **ZoneManager** - adicione permissoes com `grantperm`

## Suporte

- Painel: [arcapay.org](https://arcapay.org)
- Discord: [discord.gg/atlanta](https://discord.gg/atlanta)
