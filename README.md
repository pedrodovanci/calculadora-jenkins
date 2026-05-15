# Calculadora Console + Pipeline Jenkins + Inno Setup

Projeto didático que demonstra o ciclo completo de CI/CD para uma aplicação console em C#:
GitHub → Jenkins → build .NET → empacotamento com Inno Setup → instalador `.exe`.

## Estrutura do projeto

```
.
├── Calculadora/
│   ├── Calculadora.csproj   # Projeto .NET 8 (console)
│   └── Program.cs           # Código da calculadora
├── installer/
│   └── setup.iss            # Script do Inno Setup
├── Jenkinsfile              # Pipeline declarativa do Jenkins
├── .gitignore
└── README.md
```

---

## 1. Testando a aplicação localmente

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd Calculadora
dotnet run
```

A calculadora pede dois números e exibe um menu com soma, subtração, multiplicação, divisão, resto e potência.

---

## 2. Publicando o código no GitHub

1. Crie um repositório novo em https://github.com/new (ex.: `calculadora-jenkins`).
2. Inicialize o repositório local e faça o primeiro push:

```powershell
git init
git add .
git commit -m "feat: calculadora console + pipeline jenkins"
git branch -M main
git remote add origin https://github.com/SEU_USUARIO/calculadora-jenkins.git
git push -u origin main
```

---

## 3. Preparando o agente Jenkins (Windows)

O agente que vai executar a pipeline precisa ter:

| Ferramenta | Como instalar | Verificação |
|---|---|---|
| **.NET 8 SDK** | https://dotnet.microsoft.com/download/dotnet/8.0 | `dotnet --version` |
| **Inno Setup 6** | https://jrsoftware.org/isdl.php | `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /?` |
| **Git** | https://git-scm.com/download/win | `git --version` |

Se o Inno Setup foi instalado em outro caminho, ajuste a variável `INNO_COMPILER` no [Jenkinsfile](Jenkinsfile).

---

## 4. Plugins do Jenkins necessários

Em **Manage Jenkins → Plugins**, instale (a maioria já vem por padrão):

- **Git plugin**
- **Pipeline**
- **Pipeline: Stage View**
- **Workspace Cleanup**
- **Timestamper**
- (Opcional) **GitHub Branch Source** — se quiser usar webhooks/PRs

---

## 5. Criando o job no Jenkins

1. Acesse o Jenkins → **New Item**.
2. Digite o nome (ex.: `calculadora-pipeline`) e escolha **Pipeline** → **OK**.
3. Em **Pipeline**, configure:
   - **Definition:** `Pipeline script from SCM`
   - **SCM:** `Git`
   - **Repository URL:** `https://github.com/SEU_USUARIO/calculadora-jenkins.git`
   - **Credentials:** crie uma credencial do tipo *Username + Personal Access Token* do GitHub se o repositório for privado
   - **Branch:** `*/main`
   - **Script Path:** `Jenkinsfile`
4. Clique em **Save**.

---

## 6. (Opcional) Webhook para build automático no push

1. No GitHub: **Settings → Webhooks → Add webhook**
   - **Payload URL:** `http://SEU_JENKINS/github-webhook/`
   - **Content type:** `application/json`
   - **Events:** *Just the push event*
2. No job do Jenkins: marque **Build Triggers → GitHub hook trigger for GITScm polling**.

Se o Jenkins não está exposto na internet, use **Poll SCM** (`H/5 * * * *`) como alternativa.

---

## 7. Executando a pipeline

Clique em **Build Now**. A pipeline executa os estágios:

1. **Checkout** — clona o repositório do GitHub
2. **Restore** — `dotnet restore`
3. **Build** — `dotnet build -c Release`
4. **Publish** — gera `.exe` único, self-contained (não exige .NET instalado no usuário final), em `publish/`
5. **Empacotar com Inno Setup** — `ISCC.exe installer\setup.iss` produz `dist\CalculadoraSetup-1.0.0.exe`
6. **Arquivar Instalador** — disponibiliza o `.exe` na tela do build (link **Artifacts**)

Ao final, baixe o instalador clicando no artefato `CalculadoraSetup-1.0.0.exe`.

---

## 8. Versionando releases

Para subir a versão:

1. Atualize `<Version>` em [Calculadora/Calculadora.csproj](Calculadora/Calculadora.csproj).
2. Atualize `MyAppVersion` em [installer/setup.iss](installer/setup.iss).
3. Atualize `APP_VERSION` no [Jenkinsfile](Jenkinsfile) (apenas informativo).
4. Commit + push → o Jenkins gera um novo instalador automaticamente.

---

## 9. Troubleshooting comum

| Sintoma | Causa provável | Solução |
|---|---|---|
| `'dotnet' is not recognized` | .NET SDK ausente no agente | Instale e reinicie o serviço do Jenkins |
| `ISCC.exe not found` | Caminho diferente | Ajuste `INNO_COMPILER` no Jenkinsfile |
| `error CS...` em build | Falta restore ou versão errada do SDK | Confirme `dotnet --version` >= 8.0 |
| Build trava em `dotnet restore` | Proxy/firewall corporativo | Configure `NUGET_PACKAGES` e proxy no agente |
| Inno Setup falha por arquivos ausentes | Pasta `publish` vazia | Confirme que o estágio Publish rodou antes |
