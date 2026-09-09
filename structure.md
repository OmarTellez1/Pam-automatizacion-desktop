# Estructura y arquitectura — Pam Automation .NET

## Propósito

Este documento describe:

1. La estructura física que existe actualmente en el repositorio.
2. La responsabilidad asignada a cada proyecto y módulo.
3. Las dependencias permitidas entre proyectos.
4. La arquitectura objetivo al finalizar la aplicación.

Debe actualizarse cada vez que se agregue, elimine o cambie de responsabilidad un proyecto, carpeta o módulo relevante.

La raíz oficial es:

```text
C:\pam automatizacion dotnet\PamAutomation
```

La aplicación anterior ubicada en `C:\pam automatizacion` no forma parte de esta estructura. Es únicamente una referencia funcional, histórica y visual de solo lectura.

## Estado actual

La solución utiliza `PamAutomation.slnx`, .NET 10 y la plataforma `x64`. Actualmente existen los límites principales de la arquitectura, la persistencia JSON no sensible, el almacenamiento protegido de credenciales PAM y la generación TOTP; todavía no están implementadas las funciones de hora HTTP, PAM, Edge u Outlook.

Las fases construidas hasta ahora son:

- Fase 1: solución y proyectos base.
- Fase 2: presentación WPF, MVVM y pruebas de estados visuales.
- Fase 2B: alineación visual de la portada con la aplicación anterior.
- Fase 3A: modelos y validación de configuración no sensible.
- Fase 3B: contrato y persistencia JSON de configuración no sensible.
- Fase 4: almacenamiento protegido de credenciales PAM.
- Fase 5: generación TOTP RFC 6238 y evaluación de ventana segura.

Los directorios `.git`, `.vs`, `bin` y `obj` contienen metadatos o salidas generadas y se omiten en el árbol.

### Árbol actual del repositorio

```text
PamAutomation\
├── AGENTS.md
├── CODEX_BUILD_SPEC.md
├── README.md
├── LOGIC.md
├── History.md
├── structure.md
├── Fases.md
├── Subagentes.md
├── .gitignore
├── PamAutomation.slnx
│
├── PamAutomation.App\
│   ├── PamAutomation.App.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── AssemblyInfo.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── Resources\
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   └── ViewModels\
│       └── MainWindowViewModel.cs
│
├── PamAutomation.Core\
│   ├── PamAutomation.Core.csproj
│   ├── Interfaces\
│   │   ├── Configuration\
│   │   │   └── IAppSettingsStore.cs
│   │   └── Credentials\
│   │       ├── CredentialStoreErrorCode.cs
│   │       ├── CredentialStoreException.cs
│   │       └── IPamCredentialStore.cs
│   ├── Models\
│   │   ├── Configuration\
│   │   │   ├── AppSettings.cs
│   │   │   ├── FinalAction.cs
│   │   │   └── RequestSettings.cs
│   │   └── Credentials\
│   │       ├── PamCredentials.cs
│   │       └── PamCredentialUpdate.cs
│   ├── Totp\
│   │   ├── Base32SecretDecoder.cs
│   │   ├── InvalidBase32SecretException.cs
│   │   ├── TotpCode.cs
│   │   └── TotpGenerator.cs
│   └── Validation\
│       ├── AppSettingsValidator.cs
│       ├── ConfigurationValidationErrorCode.cs
│       ├── ConfigurationValidationFailure.cs
│       ├── ConfigurationValidationResult.cs
│       └── RequestSettingsValidator.cs
│
├── PamAutomation.Browser\
│   └── PamAutomation.Browser.csproj
│
├── PamAutomation.Infrastructure\
│   ├── PamAutomation.Infrastructure.csproj
│   ├── Configuration\
│   │   ├── AppSettingsDocument.cs
│   │   ├── AppSettingsPersistenceErrorCode.cs
│   │   ├── AppSettingsPersistenceException.cs
│   │   └── JsonAppSettingsStore.cs
│   └── Credentials\
│       ├── CredentialManagerEntry.cs
│       ├── CredentialNativeMethods.cs
│       ├── CredentialPayloadCodec.cs
│       ├── IWindowsCredentialApi.cs
│       ├── WindowsCredentialApi.cs
│       └── WindowsPamCredentialStore.cs
│
├── PamAutomation.Outlook\
│   └── PamAutomation.Outlook.csproj
│
└── PamAutomation.Tests\
    ├── PamAutomation.Tests.csproj
    ├── TestExplorerSmokeTests.cs
    ├── MainWindowViewModelTests.cs
    ├── Core\
    │   ├── Configuration\
    │   │   ├── AppSettingsValidatorTests.cs
    │   │   └── RequestSettingsValidatorTests.cs
    │   ├── Credentials\
    │   │   └── PamCredentialsTests.cs
    │   └── Totp\
    │       └── TotpGeneratorTests.cs
    └── Infrastructure\
        ├── Configuration\
        │   └── JsonAppSettingsStoreTests.cs
        └── Credentials\
            └── WindowsPamCredentialStoreTests.cs
```

### Documentos de la raíz

| Archivo | Responsabilidad |
|---|---|
| `AGENTS.md` | Reglas obligatorias de trabajo, seguridad, tecnología y validación; también funciona como índice para la consulta documental bajo demanda. |
| `CODEX_BUILD_SPEC.md` | Especificación canónica y fuente principal de requisitos. |
| `README.md` | Estado general, requisitos del entorno y decisiones confirmadas. |
| `LOGIC.md` | Comportamientos, reglas de negocio y contratos previstos. |
| `History.md` | Registro cronológico de decisiones y cambios relevantes. |
| `structure.md` | Inventario físico y arquitectura actual y objetivo. |
| `Fases.md` | Resumen acumulativo del alcance, ubicación e importancia de cada fase implementada. |
| `Subagentes.md` | Protocolo para coordinar un máximo de dos subagentes en unidades verticales independientes, aislar sus cambios e integrar únicamente trabajo validado. |
| `.gitignore` | Exclusiones de Git para salidas, configuración local y artefactos de desarrollo. |
| `PamAutomation.slnx` | Solución .NET que agrupa los seis proyectos en `x64` para operar mediante la CLI `dotnet`. |

## Proyectos actuales

En .NET, cada directorio que contiene un archivo `.csproj` es un proyecto o ensamblado independiente. No todo lo visible en la solución pertenece al mismo módulo.

### `PamAutomation.App`

Proyecto ejecutable WPF para `net10.0-windows`. Es el punto de entrada y la capa de presentación.

Contenido actual:

- `App.xaml` y `App.xaml.cs`: inicio de la aplicación y carga de recursos WPF.
- `MainWindow.xaml`: portada nativa de la aplicación.
- `MainWindow.xaml.cs`: inicialización mínima de la ventana; no contiene lógica de negocio.
- `ViewModels/MainWindowViewModel.cs`: estado de presentación y comandos de la portada mediante `CommunityToolkit.Mvvm`.
- `Resources/Colors.xaml`: colores y pinceles compartidos.
- `Resources/Styles.xaml`: estilos y plantillas de controles WPF.
- `AssemblyInfo.cs`: metadatos WPF del ensamblado.

Dependencias actuales:

- `CommunityToolkit.Mvvm` 8.4.2.
- Referencia a `PamAutomation.Core`.
- Referencia a `PamAutomation.Browser`.
- Referencia a `PamAutomation.Infrastructure`.
- Referencia a `PamAutomation.Outlook`.

Responsabilidad permanente:

- Vistas, diálogos y controles WPF.
- ViewModels y navegación.
- Recursos visuales y accesibilidad.
- Composición de dependencias al iniciar la aplicación.
- Adaptación entre controles WPF y contratos del dominio.

No debe contener:

- Selectores o navegación del portal PAM.
- Algoritmos TOTP.
- Acceso directo a archivos o Credential Manager desde los ViewModels.
- Automatización COM de Outlook.
- Reglas para crear solicitudes.

### `PamAutomation.Core`

Biblioteca para `net10.0`, sin dependencia de WPF o Windows. Actualmente contiene configuración, credenciales y TOTP:

- `Interfaces/Configuration`: contrato `IAppSettingsStore` para cargar y guardar configuración sin acoplar Core al sistema de archivos.
- `Interfaces/Credentials`: contrato `IPamCredentialStore` y errores estables para el almacenamiento protegido.
- `Models/Configuration`: `AppSettings`, `RequestSettings` y `FinalAction`.
- `Models/Credentials`: modelo completo de uso exclusivo de autenticación y modelo de actualización con campos opcionales.
- `Validation`: resultado, fallas, códigos estables y validadores de configuración.
- `Totp`: decodificación Base32, generación RFC 6238 y evaluación de la vigencia mínima antes del envío.

Los validadores reciben explícitamente la hora actual de Ecuador y no acceden al reloj, sistema de archivos o servicios externos.

Responsabilidad objetivo:

- Modelos y resultados del dominio.
- Interfaces que abstraen navegador, Outlook, reloj, credenciales y persistencia.
- Validación de solicitud y correo.
- Reglas TOTP y su coordinación futura con la hora confiable y la autenticación.
- Reglas de ejecución, confirmación, conflictos y duplicados.

Regla principal: `Core` no debe referenciar ningún otro proyecto de la solución.

### `PamAutomation.Browser`

Biblioteca para `net10.0`. Actualmente contiene únicamente su archivo de proyecto y referencia `PamAutomation.Core`.

Responsabilidad objetivo:

- Integración con Microsoft.Playwright para .NET.
- Lanzamiento de Microsoft Edge instalado mediante `Channel = "msedge"`.
- Contexto temporal y aislado por ejecución.
- Restricción de navegación al origen PAM autorizado.
- Autenticación, Favorites, solicitudes y lectura paginada de Pending.
- Selectores del portal centralizados.

No debe administrar configuración local, Outlook ni elementos WPF.

### `PamAutomation.Infrastructure`

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa actualmente la persistencia no sensible y el almacenamiento protegido de credenciales:

- `Configuration/AppSettingsDocument.cs`: representación JSON plana y conversión explícita hacia y desde el modelo de Core.
- `Configuration/JsonAppSettingsStore.cs`: carga desde la ruta privada del usuario y escritura atómica mediante un temporal en la misma carpeta.
- `Configuration/AppSettingsPersistenceErrorCode.cs` y `AppSettingsPersistenceException.cs`: errores controlados para contenido inválido, esquema incompatible y fallos de lectura o escritura.
- `Credentials/WindowsPamCredentialStore.cs`: implementación del contrato de Core mediante una única credencial genérica del usuario actual.
- `Credentials/CredentialPayloadCodec.cs`: formato binario versionado que mantiene juntas contraseña y semilla TOTP y aplica el límite del blob de Windows.
- `Credentials/WindowsCredentialApi.cs`, `CredentialNativeMethods.cs` y `CredentialManagerEntry.cs`: límite interno sobre `CredReadW`, `CredWriteW`, `CredDeleteW` y `CredFree`, con liberación y limpieza de memoria.
- `Credentials/IWindowsCredentialApi.cs`: punto interno sustituible utilizado exclusivamente por pruebas; no forma parte del dominio público.

No se agregaron paquetes NuGet para Credential Manager. El proyecto utiliza las API Win32 incluidas en Windows y expone sus tipos internos a `PamAutomation.Tests` únicamente para validar el adaptador sin tocar el almacén real.

Responsabilidad objetivo:

- Windows Credential Manager para usuario, contraseña y semilla TOTP.
- DPAPI `CurrentUser` para contenido sensible de correo.
- Lectura, validación, migración y escritura atómica de configuración.
- Implementaciones del reloj del sistema y zona horaria de Ecuador.
- Detección de dependencias instaladas cuando corresponda.
- Diagnóstico explícito de desarrollo, sin persistencia operativa en producción.

No debe conocer controles WPF, selectores PAM ni objetos COM de Outlook.

### `PamAutomation.Outlook`

Biblioteca para `net10.0-windows`. Actualmente contiene únicamente su archivo de proyecto y referencia `PamAutomation.Core`.

Responsabilidad objetivo:

- Integración con Outlook clásico mediante Interop/COM.
- Resolución exacta de cuenta por `SmtpAddress`.
- Creación, validación y envío de mensajes.
- Verificación manual de la cuenta mediante correo de prueba.
- Consulta de Enviados, Bandeja de salida y Borradores para evitar duplicados.
- Liberación explícita de referencias COM y ejecución STA.

No debe contener vistas WPF, navegación PAM ni almacenamiento de credenciales PAM.

### `PamAutomation.Tests`

Proyecto MSTest para `net10.0-windows`. Actualmente referencia `PamAutomation.App`, `PamAutomation.Core` y `PamAutomation.Infrastructure`.

Contenido actual:

- `TestExplorerSmokeTests.cs`: comprueba el descubrimiento del proyecto por el runner MSTest.
- `MainWindowViewModelTests.cs`: valida el estado inicial seguro, el bloqueo durante una ejecución y las condiciones del botón de prueba.
- `Core/Configuration/AppSettingsValidatorTests.cs`: valida valores predeterminados, configuración general y propagación de errores.
- `Core/Configuration/RequestSettingsValidatorTests.cs`: valida fecha, hora, duración y razón de la solicitud.
- `Core/Credentials/PamCredentialsTests.cs`: valida campos obligatorios y que los modelos sensibles no expongan valores al convertirse en texto.
- `Core/Totp/TotpGeneratorTests.cs`: valida vectores públicos RFC 6238, Base32, ventanas de 30 segundos, vigencia mínima y redacción del código.
- `Infrastructure/Configuration/JsonAppSettingsStoreTests.cs`: valida formato plano, carga, sustitución atómica, cancelación segura y errores de persistencia sin tocar la configuración real del usuario.
- `Infrastructure/Credentials/WindowsPamCredentialStoreTests.cs`: valida creación, actualización parcial, límites, corrupción, borrado y limpieza de buffers mediante una API de Windows falsa.

Responsabilidad objetivo:

- Pruebas unitarias de Core y ViewModels.
- Dobles de prueba para navegador, archivos, reloj y Outlook.
- Simulaciones del portal sin credenciales reales.
- Pruebas de integración separadas y explícitamente autorizadas.

Las pruebas automatizadas no deben crear solicitudes PAM ni enviar correos.

## Dependencias permitidas

La dirección de dependencias objetivo es:

```text
                         ┌──────────────────────┐
                         │  PamAutomation.App   │
                         │ WPF + MVVM + inicio  │
                         └──────┬────┬────┬─────┘
                                │    │    │
                 ┌──────────────┘    │    └──────────────┐
                 ▼                   ▼                   ▼
       PamAutomation.Browser  PamAutomation.Infrastructure  PamAutomation.Outlook
                 │                   │                   │
                 └───────────────────┼───────────────────┘
                                     ▼
                            PamAutomation.Core
                         modelos + reglas + contratos

PamAutomation.Tests ──► proyectos necesarios para cada prueba
```

Reglas:

- `Core` no depende de ninguna capa externa.
- `Browser`, `Infrastructure` y `Outlook` dependen de contratos definidos en `Core`.
- Estas tres implementaciones no deben depender entre sí.
- `App` ensambla y consume las implementaciones, pero no absorbe sus responsabilidades.
- `Tests` puede referenciar el proyecto concreto que necesite validar.

## Estructura objetivo

Los nombres finales podrán ajustarse al implementar cada fase, pero las responsabilidades y la dirección de dependencias deben conservarse.

```text
PamAutomation\
├── documentación y solución
│   ├── AGENTS.md
│   ├── CODEX_BUILD_SPEC.md
│   ├── README.md
│   ├── LOGIC.md
│   ├── History.md
│   ├── structure.md
│   ├── Subagentes.md
│   └── PamAutomation.slnx
│
├── PamAutomation.App\
│   ├── Views\
│   │   ├── MainWindow.xaml
│   │   ├── CredentialsView.xaml
│   │   ├── RequestSettingsView.xaml
│   │   ├── MailSettingsView.xaml
│   │   ├── ResultView.xaml
│   │   └── Dialogs\
│   │       ├── RealRunConfirmationDialog.xaml
│   │       ├── ErrorDialog.xaml
│   │       └── TestMailConfirmationDialog.xaml
│   ├── ViewModels\
│   │   ├── MainWindowViewModel.cs
│   │   ├── CredentialsViewModel.cs
│   │   ├── RequestSettingsViewModel.cs
│   │   ├── MailSettingsViewModel.cs
│   │   └── ResultViewModel.cs
│   ├── Resources\
│   │   ├── Colors.xaml
│   │   ├── Styles.xaml
│   │   └── Icons.xaml
│   ├── Navigation\
│   ├── MailEditing\
│   ├── App.xaml
│   └── App.xaml.cs
│
├── PamAutomation.Core\
│   ├── Models\
│   │   ├── Configuration\
│   │   ├── Requests\
│   │   ├── Mail\
│   │   └── Execution\
│   ├── Interfaces\
│   ├── Validation\
│   ├── Totp\
│   ├── BusinessRules\
│   └── Execution\
│
├── PamAutomation.Browser\
│   ├── Sessions\
│   ├── Authentication\
│   ├── Favorites\
│   ├── Requests\
│   ├── Navigation\
│   └── Selectors\
│
├── PamAutomation.Infrastructure\
│   ├── Credentials\
│   ├── Configuration\
│   │   └── Migrations\
│   ├── Files\
│   ├── Time\
│   ├── SystemChecks\
│   └── Diagnostics\
│
├── PamAutomation.Outlook\
│   ├── Accounts\
│   ├── Composition\
│   ├── Delivery\
│   ├── DuplicateDetection\
│   └── Interop\
│
├── PamAutomation.Tests\
│   ├── App\
│   ├── Core\
│   ├── Browser\
│   ├── Infrastructure\
│   ├── Outlook\
│   ├── Fakes\
│   └── Integration\
│
└── PamAutomation.Installer\               futuro, formato por decidir
```

No se deben crear carpetas vacías por adelantado. Cada submódulo aparecerá únicamente cuando una fase implemente una responsabilidad real.

## Flujo objetivo de ejecución

```text
Usuario
  │
  ▼
App / ViewModel
  │ valida entrada mediante Core
  ├──► Infrastructure ──► configuración protegida y credenciales
  ├──► Browser ─────────► Edge externo ──► portal PAM autorizado
  └──► Outlook ─────────► Outlook clásico, solo cuando corresponde
  │
  ▼
Resultado de Core mantenido en memoria
  │
  ▼
Vista de resultados WPF
```

El flujo real deberá coordinarse mediante interfaces de `Core`. La interfaz no llamará directamente a Playwright, Credential Manager, DPAPI o COM.

## Persistencia objetivo fuera del repositorio

La aplicación instalada solo podrá mantener esta configuración privada por usuario:

```text
%LOCALAPPDATA%\PamAutomation\
└── config\
    ├── app-settings.json
    └── mail-settings.dat
```

- `app-settings.json`: configuración no secreta, con `schemaVersion` y escritura atómica.
- `mail-settings.dat`: configuración de correo protegida con DPAPI `CurrentUser`.
- Usuario, contraseña y semilla TOTP: Windows Credential Manager.
- Resultados, Request ID, conflictos y estado: únicamente en memoria.

Durante desarrollo podrán existir `results`, `logs`, `artifacts` y `temp` en la raíz del repositorio, excluidos de Git y sin secretos. No forman parte de la arquitectura persistente del producto publicado.

## Límites que deben conservarse

- Solo C#, .NET 10, WPF y XAML para la aplicación.
- Playwright para .NET utilizará Edge externo con contexto temporal.
- Outlook clásico se integrará exclusivamente mediante COM.
- No se incluirán JavaScript, Node.js, Python, Java, PowerShell, Electron, WebView2, Selenium ni Chromium.
- La aplicación anterior nunca será una dependencia ni se modificará.
- La lógica de negocio permanecerá fuera del code-behind.
- No se crearán módulos compartidos genéricos sin una responsabilidad concreta.
- No se introducirán dependencias entre capas únicamente para reutilizar código menor.

## Criterio para actualizar este documento

Al terminar cada fase se debe registrar:

- Nuevos proyectos o carpetas creados.
- Responsabilidad de cada módulo nuevo.
- Referencias agregadas o eliminadas entre proyectos.
- Dependencias NuGet relevantes.
- Diferencias entre la estructura planeada y la implementada.
- Elementos objetivo que ya pasaron a estado actual.
