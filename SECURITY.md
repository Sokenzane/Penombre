# Politique de sécurité

## Sommaire

- [Signaler une faille](#signaler-une-faille)
- [Vérifier un téléchargement](#vérifier-un-téléchargement)

## Signaler une faille

N'ouvre pas de ticket public. Utilise [**Report a vulnerability**](https://github.com/Sokenzane/Penombre/security/advisories/new) (onglet Security) pour contacter le mainteneur en privé.

## Vérifier un téléchargement

Ne télécharge `Penombre.exe` que depuis les [Releases](https://github.com/Sokenzane/Penombre/releases) de ce dépôt. Chaque version indique le SHA-256 de l'exe ; pour le vérifier, lance dans PowerShell :

```powershell
Get-FileHash .\Penombre.exe -Algorithm SHA256
```
