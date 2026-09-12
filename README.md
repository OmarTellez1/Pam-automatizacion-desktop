# Pam Automation .NET

Aplicación de escritorio Windows para automatizar BeyondInsight Password Safe desde una solución construida desde cero exclusivamente con C# y .NET.

## Estado actual

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation`.

La solución `PamAutomation.slnx` contiene la aplicación WPF `PamAutomation.App` y los proyectos base `PamAutomation.Core`, `PamAutomation.Browser`, `PamAutomation.Outlook`, `PamAutomation.Infrastructure` y `PamAutomation.Tests`. Las fases 3A y 3B incorporan el dominio, la validación y la persistencia JSON de la configuración no sensible; las fases 4 a 7 incorporan credenciales protegidas, TOTP, reloj HTTP y sesión restringida de Edge. Las fases 8 a 13 agregan autenticación simulada, login real, lectura paginada de Favorites, formularios seguros, solicitudes confirmadas y lectura/comparación de Pending; la fase 14 incorpora Outlook clásico, la fase 15 la composición HTML saneada, la fase 16 conecta el flujo PAM con la interfaz, la fase 16.5 completa la paridad funcional, la 16.6 añade logging seguro, la 16.7 corrige la activación COM, la 16.8 mejora el estado visual, la fase 17 permite exportar diagnóstico seguro, la fase 18 produce la aplicación portable versionada y las fases 18.5–18.11 completan los ajustes previos a las pruebas finales. Las fases 21–21.4 añaden acceso interactivo mediante el Edge normal del usuario, confianza manual del certificado, zoom verificado al 67 %, automatización de solicitudes oculta y un preflight para repetir solicitudes Pending de manera explícita.

`PamAutomation.Tests` utiliza MSTest con Microsoft Testing Platform e incluye pruebas de presentación, configuración DPAPI, credenciales, TOTP, reloj HTTP, navegador, autenticación, `Favorites`, formularios, Pending, resultados en memoria, diagnóstico, actualización, acceso interactivo persistente, conversión `FlowDocument`, importación de firmas, composición HTML, imágenes CID y Outlook. Las fases 1 a 18.11 y 21–21.4 fueron compiladas y validadas con `Release | x64`; la suite ordinaria contiene 321 pruebas. La reconciliación mantiene exactos `Request Start Date`, `Expires`, `Description`, `System` y `Account`, reconoce las familias controladas `NOSQLBOOSTER`, `PLSQL`, `PuTTY` y `WinSCP` cuando Pending añade versión o destino a `Application`, y excluye la línea base cuando el usuario autoriza solicitudes repetidas.

Todo el flujo de desarrollo se administra exclusivamente desde la terminal con el SDK x64 de .NET 10: restauración, compilación, pruebas, ejecución y publicación.

## Objetivo

La aplicación permitirá:

- Configurar credenciales PAM de forma protegida.
- Generar TOTP usando la hora HTTP del portal.
- Iniciar sesión en BeyondInsight mediante Playwright .NET.
- Utilizar Microsoft Edge instalado externamente.
- Procesar las `Favorites` rows mediante su identidad `System + Application + Account`.
- Ejecutar un modo de prueba que nunca cree solicitudes.
- Crear solicitudes reales tras confirmación explícita.
- Leer solicitudes Pending, paginación y conflictos.
- Mostrar Request ID y resultados durante la ejecución.
- Enviar opcionalmente mediante Outlook clásico.
- Distribuirse como aplicación Windows autocontenida.

El modo de prueba estará deshabilitado por defecto mediante `testModeEnabled`. El usuario podrá cambiarlo con `Habilitar modo de prueba` dentro de una sección avanzada de Configuración de solicitud, sin editar manualmente el JSON. Cuando se habilite, su botón solo será visible si las configuraciones necesarias están completas. Si el usuario elige enviar por correo un resultado de prueba, el único destinatario permitido será `tellezoe@hitss.com`; no se utilizarán Para ni CC configurados y se exigirá confirmación explícita.

`Editar credenciales` separa las tarjetas `Credenciales del PAM` y `Cuenta de Outlook`. La primera permite guardar y probar un login no destructivo; la segunda guarda y verifica la cuenta clásica y ofrece, tras confirmación, un correo de prueba desde y hacia la misma dirección, sin CC. La plantilla, destinatarios y marcador `{{REQUEST_IDS}}` se administran aparte en `Editar envío de correo`.

El texto y la firma del correo se componen en otra aplicación y se pegan conservando el formato compatible. Las tablas no deben pegarse: `Insertar tabla 2×2` crea en la posición del cursor la estructura de solicitudes y motivo, incorpora un único `{{REQUEST_IDS}}` y deja una línea posterior editable. El botón independiente `Insertar {{REQUEST_IDS}}` continúa disponible.

Las operaciones de guardado y comprobación muestran un indicador circular animado, un mensaje inmediato de solicitud recibida y controles temporalmente deshabilitados. El indicador desaparece al finalizar. El correo de prueba solo muestra éxito cuando Outlook acepta `Send()`; un resultado `NotConfirmed` se presenta como error y no como confirmación verde.

## Documentación

- AGENTS.md: reglas permanentes que Codex carga normalmente en cada tarea e índice para consultar los demás documentos bajo demanda. La lectura completa de esos otros Markdown se realiza únicamente la primera vez que un thread entra al proyecto sin contexto previo; nuevas tareas dentro del mismo thread no la reinician.
- CODEX_BUILD_SPEC.md: especificación completa y fuente principal de decisiones.
- LOGIC.md: lógica prevista y contratos del sistema.
- History.md: historial propio de este proyecto nuevo.
- structure.md: estructura física actual, responsabilidades de proyectos y arquitectura objetivo.
- Fases.md: resumen acumulativo, dependencias entre fases pendientes y combinaciones de trabajo paralelo recomendadas.
- Subagentes.md: protocolo para un máximo de dos subagentes, con unidades verticales independientes, contexto acotado, aislamiento, pruebas propias, integración y control de consumo.
- PAM_NAVIGATION.md: recorrido confirmado del portal, selectores e invariantes, y diseño preliminar de colecciones personalizadas posteriores a la migración.

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

La aplicación no tendrá auditoría local ni debugging persistente en el uso normal. Durante cada proceso conserva como máximo 500 eventos técnicos seguros en memoria; se pierden al cerrar salvo que el usuario pulse `Exportar diagnóstico` y elija manualmente un ZIP. El logging en archivo se activa únicamente al ejecutar con un depurador conectado o con `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1`, y solo cuando el proceso puede confirmar la raíz del repositorio mediante `.git` y `PamAutomation.slnx`.

El ZIP manual contiene `manifest.json`, `environment.json`, `execution-summary.json` y `diagnostic.log`. Solo incluye versiones, arquitectura, estados, conteos, plantillas de eventos, tipo de excepción, `HRESULT` y stack trace sin rutas de código. Nunca contiene credenciales, TOTP, cookies, tokens, Request ID, identidades del portal, direcciones de correo, razón, asunto, HTML, imágenes o adjuntos. Si una operación falla, la interfaz recomienda exportarlo; ante una excepción WPF fatal se ofrece exportarlo antes del cierre seguro.

Windows o Playwright pueden crear archivos internos momentáneos en `%TEMP%` durante la ejecución. No forman parte del almacenamiento persistente de la aplicación, no deben contener secretos y deben limpiarse al finalizar cuando sea técnicamente posible.

### Datos permitidos durante el desarrollo

El repositorio de desarrollo sí podrá generar información técnica para que el desarrollador analice fallos. Esos archivos estarán en la raíz del repositorio y no formarán parte de la instalación ni de la publicación para usuarios:

    C:\pam automatizacion dotnet\PamAutomation\
    ├── results\
    ├── logs\
    ├── artifacts\
    └── temp\

Estas carpetas serán exclusivas de las ejecuciones de desarrollo, pruebas e integración. Deben estar excluidas de Git, no deben copiarse a la publicación y nunca deben contener contraseñas, semillas TOTP, códigos TOTP, cookies, tokens ni perfiles del navegador. La aplicación publicada debe desactivar esta escritura en la raíz del repositorio y mantener únicamente los resultados en memoria.

Los logs de desarrollo se escriben como `logs\pam-automation-AAAA-MM-DD.log`. Conservan niveles, categoría, etapa, `EventId`, tipo de excepción, `HRESULT` y stack trace, pero omiten deliberadamente el mensaje de la excepción para evitar que un adaptador incluya valores sensibles. Al iniciar el logger y antes de cada escritura se eliminan exclusivamente sus archivos diarios con más de dos días de antigüedad. Las direcciones, credenciales, TOTP, cookies, tokens, Request ID y contenido del correo no forman parte de los eventos.

En Visual Studio se activan al usar **Iniciar depuración (F5)**. **Iniciar sin depurar (Ctrl+F5)** requiere definir previamente `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1` en el entorno del proceso.

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
dotnet publish PamAutomation.App\PamAutomation.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=Portable-x64 -o artifacts\publish\final\PAM-Automation-0.0.1
```

`PamAutomation.Tests` usa el runner integrado de Microsoft Testing Platform. Mientras la solución no defina el runner MTP mediante `global.json`, las pruebas se ejecutan con `dotnet run` sobre el proyecto de pruebas y no con `dotnet test`.

No se requieren .NET MAUI, WinUI, C++, ASP.NET, Azure, Node.js, Python, Java, runtimes independientes ni herramientas web para compilar esta solución.

## Plataforma objetivo

- Windows x64.
- Microsoft Edge instalado.
- Outlook clásico instalado solo si se habilita el envío.
- Conectividad a la red o VPN de PAM.
- Publicación autocontenida para no exigir .NET instalado en el equipo final.

El entregable será `PAM Automation.exe`, un ejecutable portable Windows x64 autocontenido que el usuario guardará donde prefiera y reemplazará manualmente cuando descargue una nueva versión. Incluirá la lógica de la aplicación, WPF, Playwright .NET, TOTP, configuración, validaciones, resultados en memoria e integración con Outlook. No exigirá instalar .NET, Node.js, Python ni PowerShell en el equipo final.

La versión actual es `0.0.1` y aparece en el título de WPF y en las propiedades nativas del archivo. Al iniciar, la aplicación intenta leer durante un máximo de ocho segundos el `latest.json` público de GitHub mediante su URL `raw`; si contiene una versión superior y la dirección exacta del repositorio esperado, muestra un aviso descartable. No descarga ni elimina archivos.

### Publicar una nueva versión portable

1. Actualizar `Version`, `AssemblyVersion`, `FileVersion` e `InformationalVersion` en `PamAutomation.App/PamAutomation.App.csproj`. Para `0.0.2`, las versiones de ensamblado y archivo serán `0.0.2.0`.
2. Restaurar, compilar y validar desde la raíz:

   ```powershell
   dotnet restore PamAutomation.slnx -r win-x64 -p:Platform=x64
   dotnet build PamAutomation.slnx -c Release -p:Platform=x64 --no-restore
   dotnet run --project PamAutomation.Tests\PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build
   ```

3. Generar el ejecutable, sustituyendo `0.0.2` por la versión nueva:

   ```powershell
   dotnet publish PamAutomation.App\PamAutomation.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=Portable-x64 --no-restore -o artifacts\publish\final\PAM-Automation-0.0.2
   ```

4. En GitHub, crear el tag y release `v0.0.2`, adjuntar `artifacts\publish\final\PAM-Automation-0.0.2\PAM Automation.exe` como asset y publicar el release. El ejecutable autocontenido excede el límite de un archivo normal del repositorio, por lo que no debe añadirse al commit.
5. Solo después de publicar el asset, actualizar `latest.json` en `main` con la versión nueva, la fecha ISO y `repositoryUrl` igual a `https://github.com/OmarTellez1/Pam-automation-installer`. Este orden evita anunciar una versión que todavía no se puede descargar.

La aplicación seguirá requiriendo deliberadamente Windows, Microsoft Edge instalado, conectividad al portal PAM y Outlook clásico instalado cuando se active el correo. Edge y Outlook no se incluirán dentro del ejecutable porque deben utilizar las instalaciones corporativas existentes.

La automatización utilizará la opción 1: abrirá el Edge instalado mediante Playwright y creará un contexto temporal de automatización. No seleccionará, reutilizará ni modificará perfiles existentes, cookies, extensiones o sesiones del usuario. No utilizará LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. La aplicación no debe mostrar una ventana de incógnito; el aislamiento es interno de Playwright.

La automatización temporal ejecuta Edge en modo `headless`: autenticación, lectura de `Favorites`, formularios, envíos y reconciliación de `Pending` ocurren en segundo plano. El único navegador visible es el Edge normal abierto por `Acceder a PAM`, que sigue fuera del contexto temporal y permanece bajo control del usuario.

## Decisiones de implementación confirmadas

- Framework de pruebas: MSTest con Microsoft Testing Platform y runner integrado ejecutado desde la terminal.
- Presentación: MVVM con CommunityToolkit.Mvvm; los estados y comandos se mantienen fuera del code-behind. La aplicación anterior es la referencia visual para la portada y los formularios, reconstruidos con WPF/XAML nativo.
- Solución: `PamAutomation.slnx`.
- Credenciales PAM: una credencial genérica versionada en Windows Credential Manager, vinculada al usuario actual y sin archivos de secretos.
- Navegador automático: Edge instalado, `Channel = "msedge"`, contexto temporal y ejecución `headless`; `Acceder a PAM` utiliza una ventana normal visible separada.
- Protección contra correo duplicado: estado en memoria durante la ejecución y consulta de Enviados, Bandeja de salida y Borradores de Outlook antes de enviar; no se crearán archivos locales `last-email` o `delivery-state`.
- Outlook COM: activación tardía del `ProgID` corporativo `Outlook.Application`, sin PIA administrada ni dependencia en `office.dll`; el acceso permanece encapsulado, ejecutado en STA y con liberación explícita.
- Editor de correo: `RichTextBox` y `FlowDocument` como editor nativo, con un subconjunto explícito de formato y un convertidor C# propio a HTML saneado. El contenido enriquecido y las imágenes se persistirán únicamente dentro de `mail-settings.dat` protegido con DPAPI.

## Próximo paso

Ejecutar la fase 19 en un equipo Windows administrado sin SDK de .NET, utilizando el ejecutable portable ya validado y el release publicado en GitHub.
