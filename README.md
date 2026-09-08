# Pam Automation .NET

Aplicación de escritorio Windows para automatizar BeyondInsight Password Safe desde una solución construida desde cero exclusivamente con C# y .NET.

## Estado actual

Este repositorio contiene inicialmente solo documentación de diseño. Todavía no se han creado la solución .NET, la aplicación WPF ni los proyectos de código.

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

## Documentación

- AGENTS.md: reglas de trabajo para Codex.
- CODEX_BUILD_SPEC.md: especificación completa y fuente principal de decisiones.
- LOGIC.md: lógica prevista y contratos del sistema.
- History.md: historial propio de este proyecto nuevo.

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

La configuración no secreta se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\app-settings.json

Incluirá la acción final, razón, duración, fecha, hora y zona horaria. La fecha se persistirá internamente en formato ISO, aunque la interfaz podrá mostrarla como dd/MM.

La configuración de correo se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat

Ese archivo contendrá remitente, destinatarios, copias, asunto y contenido HTML. Se protegerá con DPAPI CurrentUser porque el contenido del correo puede ser sensible.

La configuración anterior es privada y específica del usuario. Los archivos operativos que un usuario normal no necesita leer no se guardarán en su perfil, sino en una carpeta común de datos de la máquina:

La estructura persistente completa queda limitada a:

    %LOCALAPPDATA%\PamAutomation\
    └── config\
        ├── app-settings.json
        └── mail-settings.dat

No crear ni mantener carpetas persistentes de `results`, `logs`, `artifacts` o `temp` en `%PROGRAMDATA%`, `%LOCALAPPDATA%` ni junto al ejecutable. Los resultados de una ejecución deben vivir en memoria y mostrarse en la interfaz mientras la aplicación esté abierta. Si la acción final es correo, el envío se realiza dentro de la misma ejecución; no se necesita conservar un `last-run` para completar la operación.

La aplicación no tendrá auditoría local ni debugging persistente en el uso normal. Si el desarrollador necesita diagnóstico, deberá activarse explícitamente y exportarse manualmente a una ubicación elegida por el usuario; no será una función automática ni una dependencia del flujo normal.

Windows o Playwright pueden crear archivos internos momentáneos en `%TEMP%` durante la ejecución. No forman parte del almacenamiento persistente de la aplicación, no deben contener secretos y deben limpiarse al finalizar cuando sea técnicamente posible.

### Datos permitidos durante el desarrollo

El repositorio de desarrollo sí podrá generar información técnica para que el desarrollador analice fallos. Esos archivos estarán en la raíz del repositorio y no formarán parte de la instalación ni de la publicación para usuarios:

    C:\pam automatizacion dotnet\
    ├── results\
    ├── logs\
    ├── artifacts\
    └── temp\

Estas carpetas serán exclusivas de las ejecuciones de desarrollo, pruebas e integración. Deben estar excluidas de Git, no deben copiarse al instalador y nunca deben contener contraseñas, semillas TOTP, códigos TOTP, cookies, tokens ni perfiles del navegador. La aplicación publicada debe desactivar esta escritura en la raíz del repositorio y mantener únicamente los resultados en memoria.

## Instalación de Visual Studio para desarrollar

En Visual Studio Installer instalar únicamente la carga de trabajo **Desarrollo de escritorio de .NET**. Esta carga es la necesaria para crear y depurar la aplicación WPF con C# y .NET. El proyecto debe seleccionar el SDK de .NET 10 LTS cuando esté disponible en la instalación.

No es necesario instalar para este proyecto:

- Desarrollo de la interfaz de usuario de aplicaciones multiplataforma (.NET MAUI).
- Desarrollo de aplicaciones WinUI.
- Desarrollo para escritorio con C++.
- Desarrollo móvil con C++.
- Desarrollo de ASP.NET y web.
- Desarrollo de Azure, Node.js, Python, Java o herramientas web.
- Entity Framework 6, ML.NET Model Builder, WCF o componentes de compatibilidad de .NET Framework, salvo que otra dependencia corporativa los exija.

Dentro de los componentes opcionales de **Desarrollo de escritorio de .NET**, no es necesario agregar Blend, IntelliCode, GitHub Copilot, .NET Copilot Skills, Modernización de GitHub Copilot ni herramientas de perfiles para que la aplicación compile. Pueden instalarse por preferencia del desarrollador, pero no son dependencias del producto.

La carga recomendada corresponde a la guía oficial de [crear una aplicación WPF con C#](https://learn.microsoft.com/en-us/visualstudio/get-started/csharp/tutorial-wpf?view=visualstudio) y al listado oficial de componentes de [Desarrollo de escritorio de .NET](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-professional).

## Plataforma objetivo

- Windows x64.
- Microsoft Edge instalado.
- Outlook clásico instalado solo si se habilita el envío.
- Conectividad a la red o VPN de PAM.
- Publicación autocontenida para no exigir .NET instalado en el equipo final.

El entregable final será un ejecutable Windows x64 autocontenido, preferiblemente distribuido mediante un instalador firmado. Incluirá la lógica de la aplicación, WPF, Playwright .NET, TOTP, configuración, validaciones, resultados en memoria e integración con Outlook. No exigirá instalar .NET, Node.js, Python ni PowerShell en el equipo final.

La aplicación seguirá requiriendo deliberadamente Windows, Microsoft Edge instalado, conectividad al portal PAM y Outlook clásico instalado cuando se active el correo. Edge y Outlook no se incluirán dentro del ejecutable porque deben utilizar las instalaciones corporativas existentes.

La automatización utilizará la opción 1: abrirá el Edge instalado mediante Playwright y creará un contexto temporal de automatización. No seleccionará, reutilizará ni modificará perfiles existentes, cookies, extensiones o sesiones del usuario. No utilizará LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. La aplicación no debe mostrar una ventana de incógnito; el aislamiento es interno de Playwright.

## Próximo paso

Antes de escribir código, Codex debe analizar la documentación nueva y la aplicación anterior como referencia de solo lectura, informar contradicciones y proponer un plan por fases.
