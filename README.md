# Pam Automation .NET

Aplicación de escritorio Windows para automatizar BeyondInsight Password Safe desde una solución construida desde cero exclusivamente con C# y .NET.

## Estado actual

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation`.

La solución `PamAutomation.slnx` contiene la aplicación WPF `PamAutomation.App` y los proyectos base `PamAutomation.Core`, `PamAutomation.Browser`, `PamAutomation.Outlook`, `PamAutomation.Infrastructure` y `PamAutomation.Tests`. Las fases 3A y 3B incorporan el dominio, la validación y la persistencia JSON de la configuración no sensible. La fase 4 incorpora el almacenamiento protegido de credenciales PAM en Windows Credential Manager. La fase 5 incorpora la generación TOTP RFC 6238 y la evaluación de su ventana segura. Aún no existe implementación funcional de hora HTTP, PAM, Edge u Outlook y la interfaz todavía no está conectada a estos servicios.

`PamAutomation.Tests` utiliza MSTest con Microsoft Testing Platform e incluye pruebas del estado de presentación, validación, persistencia de configuración, almacenamiento protegido simulado y TOTP con vectores públicos. Las fases 1, 2, 2B, 3A, 3B, 4 y 5 fueron compiladas y validadas con `Release | x64`. La fase 5 tiene 71 pruebas aprobadas. El diseño 2B fue aprobado funcional y visualmente, aunque queda pendiente reducir la escala de sus elementos.

Todo el flujo de desarrollo se administra exclusivamente desde la terminal con el SDK x64 de .NET 10: restauración, compilación, pruebas, ejecución y publicación.

## Objetivo

La aplicación permitirá:

- Configurar credenciales PAM de forma protegida.
- Generar TOTP usando la hora HTTP del portal.
- Iniciar sesión en BeyondInsight mediante Playwright .NET.
- Utilizar Microsoft Edge instalado externamente.
- Procesar las cuentas de Favorites.
- Ejecutar un modo de prueba que nunca cree solicitudes.
- Crear solicitudes reales tras confirmación explícita.
- Leer solicitudes Pending, paginación y conflictos.
- Mostrar Request ID y resultados durante la ejecución.
- Enviar opcionalmente mediante Outlook clásico.
- Distribuirse como aplicación Windows autocontenida.

El modo de prueba estará deshabilitado por defecto mediante `testModeEnabled`. El usuario podrá cambiarlo con `Habilitar modo de prueba` dentro de una sección avanzada de Configuración de solicitud, sin editar manualmente el JSON. Cuando se habilite, su botón solo será visible si las configuraciones necesarias están completas. Si el usuario elige enviar por correo un resultado de prueba, el único destinatario permitido será `tellezoe@hitss.com`; no se utilizarán Para ni CC configurados y se exigirá confirmación explícita.

La configuración de Outlook tendrá además una comprobación manual independiente: `Enviar correo de prueba` podrá enviar un mensaje a la misma cuenta SMTP que el usuario acaba de configurar, previa confirmación. Esta acción valida Outlook y no debe confundirse con el correo de resultados del modo de prueba PAM.

## Documentación

- AGENTS.md: reglas permanentes que Codex carga normalmente en cada tarea e índice para consultar los demás documentos bajo demanda. La lectura completa de esos otros Markdown se realiza únicamente la primera vez que un thread entra al proyecto sin contexto previo; nuevas tareas dentro del mismo thread no la reinician.
- CODEX_BUILD_SPEC.md: especificación completa y fuente principal de decisiones.
- LOGIC.md: lógica prevista y contratos del sistema.
- History.md: historial propio de este proyecto nuevo.
- structure.md: estructura física actual, responsabilidades de proyectos y arquitectura objetivo.
- Fases.md: resumen acumulativo, dependencias entre fases pendientes y combinaciones de trabajo paralelo recomendadas.
- Subagentes.md: protocolo para un máximo de dos subagentes, con unidades verticales independientes, contexto acotado, aislamiento, pruebas propias, integración y control de consumo.

## Referencia histórica

La aplicación anterior está en:

C:\pam automatizacion

Se puede leer para conocer comportamiento, reglas y diseño, pero no debe modificarse ni convertirse en dependencia del nuevo proyecto.

La nueva aplicación no debe copiar JavaScript, Node.js, PowerShell, archivos .cmd, .env ni la arquitectura anterior.

## Almacenamiento local

La aplicación no utilizará archivos .env.

Las credenciales PAM se guardarán en Windows Credential Manager, asociadas al usuario actual de Windows:

- Usuario PAM.
- Contraseña PAM.
- Semilla TOTP de Microsoft Authenticator.

La implementación utiliza una sola credencial genérica versionada para que usuario, contraseña y semilla se sustituyan juntos. Los campos vacíos durante una edición conservan el valor protegido anterior. Las pruebas automatizadas sustituyen la API de Windows por un adaptador falso y no crean entradas reales en Credential Manager.

La configuración no secreta se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\app-settings.json

Incluirá la acción final, razón, duración, fecha, hora y zona horaria. La fecha se persistirá internamente en formato ISO, aunque la interfaz podrá mostrarla como dd/MM.

La configuración de correo se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat

Ese archivo contendrá remitente, destinatarios, copias, asunto y contenido HTML. Se protegerá con DPAPI CurrentUser porque el contenido del correo puede ser sensible.

La configuración es privada y específica del usuario. La estructura persistente completa queda limitada a:

    %LOCALAPPDATA%\PamAutomation\
    └── config\
        ├── app-settings.json
        └── mail-settings.dat

No crear ni mantener carpetas persistentes de `results`, `logs`, `artifacts` o `temp` en `%PROGRAMDATA%`, `%LOCALAPPDATA%` ni junto al ejecutable. Los resultados de una ejecución deben vivir en memoria y mostrarse en la interfaz mientras la aplicación esté abierta. Si la acción final es correo, el envío se realiza dentro de la misma ejecución; no se necesita conservar un `last-run` para completar la operación.

La aplicación no tendrá auditoría local ni debugging persistente en el uso normal. Si el desarrollador necesita diagnóstico, deberá activarse explícitamente y exportarse manualmente a una ubicación elegida por el usuario; no será una función automática ni una dependencia del flujo normal.

Windows o Playwright pueden crear archivos internos momentáneos en `%TEMP%` durante la ejecución. No forman parte del almacenamiento persistente de la aplicación, no deben contener secretos y deben limpiarse al finalizar cuando sea técnicamente posible.

### Datos permitidos durante el desarrollo

El repositorio de desarrollo sí podrá generar información técnica para que el desarrollador analice fallos. Esos archivos estarán en la raíz del repositorio y no formarán parte de la instalación ni de la publicación para usuarios:

    C:\pam automatizacion dotnet\PamAutomation\
    ├── results\
    ├── logs\
    ├── artifacts\
    └── temp\

Estas carpetas serán exclusivas de las ejecuciones de desarrollo, pruebas e integración. Deben estar excluidas de Git, no deben copiarse al instalador y nunca deben contener contraseñas, semillas TOTP, códigos TOTP, cookies, tokens ni perfiles del navegador. La aplicación publicada debe desactivar esta escritura en la raíz del repositorio y mantener únicamente los resultados en memoria.

## Entorno de desarrollo por terminal

Instalar el SDK x64 de .NET 10 LTS y comprobar que `dotnet` esté disponible en `PATH`. El SDK incluye los componentes necesarios para compilar la aplicación WPF y sus pruebas; no es necesario instalar runtimes por separado.

Comprobación del entorno:

```text
dotnet --info
dotnet --list-sdks
```

Flujo canónico desde la raíz del repositorio:

```text
dotnet restore PamAutomation.slnx -p:Platform=x64
dotnet build PamAutomation.slnx -c Release -p:Platform=x64 --no-restore
dotnet run --project PamAutomation.Tests\PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build
dotnet run --project PamAutomation.App\PamAutomation.App.csproj -c Release -p:Platform=x64 --no-build
```

`PamAutomation.Tests` usa el runner integrado de Microsoft Testing Platform. Mientras la solución no defina el runner MTP mediante `global.json`, las pruebas se ejecutan con `dotnet run` sobre el proyecto de pruebas y no con `dotnet test`.

No se requieren .NET MAUI, WinUI, C++, ASP.NET, Azure, Node.js, Python, Java, runtimes independientes ni herramientas web para compilar esta solución.

## Plataforma objetivo

- Windows x64.
- Microsoft Edge instalado.
- Outlook clásico instalado solo si se habilita el envío.
- Conectividad a la red o VPN de PAM.
- Publicación autocontenida para no exigir .NET instalado en el equipo final.

El entregable final será un ejecutable Windows x64 autocontenido, preferiblemente distribuido mediante un instalador firmado. Incluirá la lógica de la aplicación, WPF, Playwright .NET, TOTP, configuración, validaciones, resultados en memoria e integración con Outlook. No exigirá instalar .NET, Node.js, Python ni PowerShell en el equipo final.

La aplicación seguirá requiriendo deliberadamente Windows, Microsoft Edge instalado, conectividad al portal PAM y Outlook clásico instalado cuando se active el correo. Edge y Outlook no se incluirán dentro del ejecutable porque deben utilizar las instalaciones corporativas existentes.

La automatización utilizará la opción 1: abrirá el Edge instalado mediante Playwright y creará un contexto temporal de automatización. No seleccionará, reutilizará ni modificará perfiles existentes, cookies, extensiones o sesiones del usuario. No utilizará LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. La aplicación no debe mostrar una ventana de incógnito; el aislamiento es interno de Playwright.

La primera versión ejecutará Edge de forma visible tanto para la validación de credenciales como para las automatizaciones reales. Se conservarán del flujo anterior sus reglas funcionales y validaciones del portal, pero no la reutilización de perfiles de Chrome ni su ejecución headless. Un modo headless solo podrá evaluarse posteriormente mediante una decisión documentada y pruebas equivalentes.

## Decisiones de implementación confirmadas

- Framework de pruebas: MSTest con Microsoft Testing Platform y runner integrado ejecutado desde la terminal.
- Presentación: MVVM con CommunityToolkit.Mvvm; los estados y comandos se mantienen fuera del code-behind. La aplicación anterior es la referencia visual para la portada y los formularios, reconstruidos con WPF/XAML nativo.
- Solución: `PamAutomation.slnx`.
- Credenciales PAM: una credencial genérica versionada en Windows Credential Manager, vinculada al usuario actual y sin archivos de secretos.
- Navegador: Edge instalado, `Channel = "msedge"`, contexto temporal y ejecución visible en la primera versión.
- Protección contra correo duplicado: estado en memoria durante la ejecución y consulta de Enviados, Bandeja de salida y Borradores de Outlook antes de enviar; no se crearán archivos locales `last-email` o `delivery-state`.
- Editor de correo: `RichTextBox` y `FlowDocument` como editor nativo, con un subconjunto explícito de formato y un convertidor C# propio a HTML saneado. El contenido enriquecido y las imágenes se persistirán únicamente dentro de `mail-settings.dat` protegido con DPAPI.

## Próximo paso

Implementar la fase 6 de hora HTTP confiable del portal. Después de cada cambio, restaurar cuando corresponda, compilar la solución en `Release | x64` y ejecutar el proyecto de pruebas desde la terminal.
