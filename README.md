# Pam Automation .NET

La ventana `Colecciones` abre maximizada desde su primera apertura.

`Publish-Portable.ps1` es parte del código versionado y se encuentra en la raíz. Se ejecuta manualmente con `./Publish-Portable.ps1 -ReplaceExisting` para restaurar, compilar Release x64, ejecutar pruebas y generar el portable y ZIP en `artifacts/publish/final`. No sube archivos a GitHub ni cambia la versión. Debe distribuirse la carpeta extraída completa, incluidas sus dependencias y `.playwright`.

`Directory.Build.targets` detiene la compilación con un error claro si falta el script. Ambos archivos deben viajar juntos en los commits y merges; una rama antigua que todavía no los contiene necesita integrar el commit de restauración. No mantener el script solo como archivo local ni excluirlo mediante `info/exclude`. Puede comprobarse su inclusión con `git ls-files --error-unmatch Publish-Portable.ps1`.

Aplicación de escritorio Windows para automatizar BeyondInsight Password Safe desde una solución construida desde cero exclusivamente con C# y .NET.

## Estado actual

La navegación de solicitudes reutiliza Favorites cuando la URL, la categoría seleccionada y el grid visible siguen siendo válidos, sin un formulario abierto. Busca primero en la página actual y resuelve cada acción Access por System + Application + Account exactos. Si la vista quedó obsoleta o falla su apertura, permite una sola recuperación desde Accounts, dentro de un presupuesto de 60 segundos. El llenado, calendario y Submit quedan fuera de ese reintento. Este ajuste del 1 de octubre se documenta en Fases.md y History.md; no implica que el fallo intermitente del calendario esté resuelto.

Validación de Favorites: Release x64 correcto, 49 pruebas relacionadas aprobadas y 14 formularios reales validados y descartados en Edge visible. No hubo recargas entre formularios; al salir deliberadamente a Requests se recuperó Favorites con una sola recarga. Cero intentos de creación y cero correos. Sin cambio de versión ni publicación.

La selección del calendario PAM usa la cabecera nativa de mes y año y elige el mes solicitado directamente en el desplegable. Espera que los días correspondan a esa cabecera, selecciona el día habilitado y confirma el valor exacto retenido. Esta revisión del 30 de septiembre reemplaza la dependencia anterior de observar todos los días del mes, después del diagnóstico `20260930-213552` con `MonthUnreadable`. Las flechas quedan para cambios de año o compatibilidad sin cabecera; no se repiten envíos. El diagnóstico conserva fechas y conteos seguros. Los resultados de validación y los límites de la reproducción están en `Fases.md` y `History.md`.

Si el calendario aparece y desaparece antes de leer el mes inicial, la aplicación puede reabrirlo una vez sobre el mismo campo de fecha, dentro del tiempo que ya queda para esa lectura. Si el campo cambió o el mes sigue sin poder confirmarse, el formulario se detiene antes de enviar. Esta recuperación no se aplica tras cambiar de mes o año.

Validación del ajuste: compilación Release x64, 60 pruebas relacionadas aprobadas, 28 formularios reales descartados (14 accesos en visible y headless) y, con autorización expresa, tres solicitudes reales consecutivas de cinco minutos con sus tres Request ID recuperados. No se enviaron correos. La condición transitoria exacta del incidente no se reprodujo; la nueva ruta del calendario sí se verificó en el portal. Sin cambio de versión ni publicación: el ejecutable portable debe regenerarse para incorporar el ajuste.

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation`.

La solución `PamAutomation.slnx` contiene la aplicación WPF `PamAutomation.App` y los proyectos base `PamAutomation.Core`, `PamAutomation.Browser`, `PamAutomation.Outlook`, `PamAutomation.Infrastructure` y `PamAutomation.Tests`. Las fases 3A y 3B incorporan el dominio, la validación y la persistencia JSON de la configuración no sensible; las fases 4 a 7 incorporan credenciales protegidas, TOTP, reloj HTTP y sesión restringida de Edge. Las fases 8 a 13 agregan autenticación simulada, login real, lectura paginada de Favorites, formularios seguros, solicitudes confirmadas y lectura/comparación de Pending; la fase 14 incorpora Outlook clásico, la fase 15 la composición HTML saneada, la fase 16 conecta el flujo PAM con la interfaz, la fase 17 permite exportar diagnóstico seguro y la fase 18 produce la aplicación portable versionada. Las fases 21–21.10 añaden acceso interactivo y robustez operativa. Las fases 20.1–20.5 incorporan el catálogo protegido, colecciones editables y eliminables, `All favorites` sincronizada y la colección activa como único alcance de automatización. La fase 22 optimiza el arranque; la fase 23 completa el usuario principal obligatorio, la administración protegida de cuentas PAM secundarias, su selección exclusiva para el acceso interactivo y la aceptación de migración y seguridad.

La fase 24 hace genérica la reconciliación de Pending: conserva la advertencia previa de duplicados, excluye los Request ID existentes antes del envío y asocia únicamente filas nuevas mediante `System + Account`, inicio, expiración, descripción y cantidades. Retiró las familias hardcodeadas de `Application` para admitir cualquiera de los aproximadamente 383 accesos disponibles sin asumir que todos se solicitan en una ejecución. Una segunda barrera impide que un ID de la línea base llegue accidentalmente al resultado o al correo. Cuando el usuario elige deliberadamente reutilizar solicitudes existentes, la aplicación exige un conjunto completo y no ambiguo, omite la creación PAM y envía exclusivamente esos IDs mediante Outlook.

La lectura de Pending selecciona primero la mayor cantidad de filas que ofrece el portal —actualmente 200— para obtener lotes habituales desde una sola página. Espera a que `Status = Pending` termine de cargar, comprueba el total publicado y conserva el recorrido paginado como respaldo cuando el total supera el máximo o el portal no expone el selector.

El diagnóstico explícito incorpora `pending-reconciliation.json` desde el inicio del preflight de Pending, incluso si nunca llega a obtener una línea base. La traza de esquema 3 distingue `Preflight` y `Reconciliation`, registra cada lectura y recuperación por sesión y conserva el error final. La línea base ausente se omite del JSON; una lectura vacía válida sí aparece como snapshot de cero filas. El archivo conserva en memoria y exporta únicamente por decisión del usuario Request ID, `System`, `Application`, `Account`, inicio, expiración, descripción, diferencias y multiplicidades. Credenciales, contraseña, semilla y código TOTP, cookies, tokens y contenido del correo permanecen excluidos.

Si falla la comprobación de un formulario PAM antes de `Submit Request`, el log exportado identifica si ocurrió tras llenar o justo antes de enviar, los campos distintos, los intentos y fallos de lectura, y los valores esperados/observados de fecha, hora, duración y `Application Session`. Del motivo solo conserva su longitud y si difiere; un valor inesperado del portal se sustituye por `UnexpectedFormat`. No se registran contraseñas, TOTP, cookies, tokens ni el texto libre del motivo.

La cabecera identifica al usuario principal por nombre e inicial. `Usuarios PAM` permite renombrar al principal y administrar hasta cuatro cuentas secundarias; el principal no puede eliminarse y los secretos de cada secundaria permanecen aislados en Windows Credential Manager.

`PamAutomation.Tests` utiliza MSTest con Microsoft Testing Platform e incluye pruebas de presentación, configuración DPAPI, credenciales, TOTP, reloj HTTP, navegador, autenticación, `Favorites`, colecciones, formularios, Pending, resultados en memoria, diagnóstico, actualización, acceso interactivo persistente, identidades PAM, composición HTML y Outlook. Las fases 1 a 18.11, 20.1–20.5 y 21–25 fueron compiladas con `Release | x64`; la suite ordinaria contiene 453 pruebas. La reconciliación mantiene exactos `Request Start Date`, `Expires`, `Description`, `System` y `Account`; la ejecución exige nuevamente las claves `System + Application + Account` de la colección activa antes de cualquier envío.

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

El modo de prueba permanece deshabilitado por defecto mediante `testModeEnabled`, pero ya no se expone como opción editable en `Configurar solicitud`. Si está habilitado en una configuración de desarrollo compatible, su botón solo será visible cuando las configuraciones necesarias estén completas. Su correo continúa limitado a `tellezoe@hitss.com`, sin utilizar Para ni CC configurados y con confirmación explícita.

`Editar credenciales` separa las tarjetas `Credenciales del PAM` y `Cuenta de Outlook`. La primera permite guardar y probar un login no destructivo; la segunda guarda y verifica la cuenta clásica y ofrece, tras confirmación, un correo de prueba desde y hacia la misma dirección, sin CC. El remitente sigue perteneciendo al usuario principal y no cambia al elegir otra colección.

La fase 26 incorpora `Configurar correo de esta colección` dentro de `Configurar colección`, también para `All favorites`. Cada colección guarda Para, CC, asunto, contenido con un único `{{REQUEST_IDS}}` e imágenes inline. Cancelar la colección descarta también su borrador de correo. La pantalla principal ofrece `Configurar firma del correo`: es opcional, común al principal y se añade una sola vez al final al preparar los envíos futuros, sin reescribir las colecciones.

La actualización copia el correo global anterior a cada colección ya guardada; las copias son independientes. Las nuevas empiezan sin correo. Si Outlook está activado, una colección sin correo válido muestra qué debe configurarse y bloquea el inicio antes de contactar PAM; con Outlook desactivado permite solicitar accesos y mostrar IDs. La ruta que reutiliza IDs existentes en Pending usa el mismo correo de la colección activa y la firma común.

La migración conserva íntegro el contenido anterior y deja vacía la nueva firma común. Si el contenido antiguo ya incluía una firma, retírala manualmente del contenido de las colecciones al configurar la firma común para evitar duplicarla; no se intenta reconocer ni recortar automáticamente texto del usuario.

El texto del correo y la firma pueden pegarse conservando el formato compatible en sus respectivos editores. En el contenido de la colección, `Insertar tabla 2×2` crea la estructura de solicitudes y motivo con un único `{{REQUEST_IDS}}`; no pegues tablas externas en ese contenido. La firma no admite marcadores de Request ID.

Las operaciones de guardado y comprobación muestran un indicador circular animado, un mensaje inmediato de solicitud recibida y controles temporalmente deshabilitados. El indicador desaparece al finalizar. El correo de prueba solo muestra éxito cuando Outlook acepta `Send()`; un resultado `NotConfirmed` se presenta como error y no como confirmación verde.

## Documentación

- AGENTS.md: reglas permanentes que Codex carga normalmente en cada tarea e índice para consultar los demás documentos bajo demanda. La lectura completa de esos otros Markdown se realiza únicamente la primera vez que un thread entra al proyecto sin contexto previo; nuevas tareas dentro del mismo thread no la reinician.
- CODEX_BUILD_SPEC.md: especificación completa y fuente principal de decisiones.
- LOGIC.md: lógica prevista y contratos del sistema.
- History.md: historial propio de este proyecto nuevo.
- structure.md: estructura física actual, responsabilidades de proyectos y arquitectura objetivo.
- Fases.md: resumen acumulativo, dependencias entre fases pendientes y combinaciones de trabajo paralelo recomendadas.
- Subagentes.md: protocolo para un máximo de dos subagentes, con unidades verticales independientes, contexto acotado, aislamiento, pruebas propias, integración y control de consumo.
- PAM_NAVIGATION.md: recorrido confirmado del portal, selectores, invariantes y contratos actuales de colecciones personalizadas.

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

Incluye la acción final, duración, fecha, hora, zona horaria y un campo de razón legado para compatibilidad. La razón efectiva procede siempre de la colección activa. La fecha se persiste internamente en formato ISO, aunque la interfaz la muestra como dd/MM.

El correo se guarda dentro de cada colección en:

    %LOCALAPPDATA%\PamAutomation\config\favorite-collections.dat

Ese archivo usa esquema 3, DPAPI CurrentUser y escritura atómica. La firma se guarda separadamente en `mail-signature.dat` y el remitente en `outlook-account.dat`, también protegidos. `mail-settings.dat` se conserva únicamente como fuente histórica de migración; nunca se usa como correo alternativo de una colección.

La configuración es privada y específica del usuario. La estructura persistente completa queda limitada a:

    %LOCALAPPDATA%\PamAutomation\
    └── config\
        ├── app-settings.json
        ├── favorite-collections.dat
        ├── mail-signature.dat
        ├── outlook-account.dat
        └── mail-settings.dat (legado conservado)

No crear ni mantener carpetas persistentes de `results`, `logs`, `artifacts` o `temp` en `%PROGRAMDATA%`, `%LOCALAPPDATA%` ni junto al ejecutable. Los resultados de una ejecución deben vivir en memoria y mostrarse en la interfaz mientras la aplicación esté abierta. Si la acción final es correo, el envío se realiza dentro de la misma ejecución; no se necesita conservar un `last-run` para completar la operación.

La aplicación no tendrá auditoría local ni debugging persistente en el uso normal. Durante cada proceso conserva como máximo 500 eventos técnicos seguros en memoria; se pierden al cerrar salvo que el usuario pulse `Exportar diagnóstico` y elija manualmente un ZIP. El logging en archivo se activa únicamente al ejecutar con un depurador conectado o con `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1`, y solo cuando el proceso puede confirmar la raíz del repositorio mediante `.git` y `PamAutomation.slnx`.

El ZIP manual contiene `manifest.json`, `environment.json`, `execution-summary.json` y `diagnostic.log`. Desde el inicio del preflight de Pending añade `pending-reconciliation.json` con los Request ID y campos de Pending autorizados expresamente para soporte. El log general solo incluye versiones, arquitectura, estados, conteos, plantillas de eventos, tipo de excepción, `HRESULT` y stack trace sin rutas de código. El paquete nunca contiene credenciales, contraseña, TOTP, cookies, tokens, direcciones de correo, asunto, HTML, imágenes o adjuntos. Si una operación falla, la interfaz recomienda exportarlo; ante una excepción WPF fatal se ofrece exportarlo antes del cierre seguro.

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
dotnet publish PamAutomation.App\PamAutomation.App.csproj -c Release -p:Platform=x64 -p:PublishProfile=Portable-x64 -o artifacts\publish\final\PAM-Automation-1.0.2
```

`PamAutomation.Tests` usa el runner integrado de Microsoft Testing Platform. Mientras la solución no defina el runner MTP mediante `global.json`, las pruebas se ejecutan con `dotnet run` sobre el proyecto de pruebas y no con `dotnet test`.

No se requieren .NET MAUI, WinUI, C++, ASP.NET, Azure, Node.js, Python, Java, runtimes independientes ni herramientas web para compilar esta solución.

## Plataforma objetivo

- Windows x64.
- Microsoft Edge instalado.
- Outlook clásico instalado solo si se habilita el envío.
- Conectividad a la red o VPN de PAM.
- Publicación autocontenida para no exigir .NET instalado en el equipo final.

El entregable será `PAM-Automation-<versión>-win-x64.zip`. Contiene una carpeta portable Windows x64 autocontenida con `PAM Automation.exe` y todas sus dependencias preextraídas. El usuario puede guardarla donde prefiera, pero debe conservar juntos todos los archivos de la carpeta. No exige instalar .NET, Node.js, Python ni PowerShell en el equipo final.

La versión estable configurada es `1.0.2` y aparece en el título de WPF y en las propiedades nativas del archivo. El ejecutable incorpora el icono oficial circular `PA`; la portada conserva su insignia azul redondeada original y muestra el monograma textual `PA`. Todas las ventanas usan una barra de título gris oscuro con texto y controles de alto contraste cuando Windows admite los atributos DWM modernos. Al iniciar, la ventana presenta el estado `Preparando PAM Automation…`, carga la configuración protegida y queda utilizable sin esperar la consulta remota. La comprobación de `latest.json` se ejecuta después en segundo plano durante un máximo de ocho segundos; si contiene una versión superior y la dirección exacta del repositorio esperado, muestra un aviso descartable. No descarga ni elimina archivos.

Cuando el preflight encuentra solicitudes idénticas en Pending, nunca permite volver a crearlas. Si existe un conjunto completo y no ambiguo para toda la colección y Outlook está habilitado, el usuario puede enviar por correo esos Request ID existentes o cancelar. Ante coincidencias parciales, ambiguas o sin correo habilitado, debe cancelar y cambiar la fecha u hora de inicio, la duración o el motivo. La elección de reutilizar crea un correo nuevo incluso si la misma huella ya fue enviada y muestra el estado devuelto por Outlook; la deduplicación permanece activa en todos los envíos ordinarios.

Si PAM procesa solicitudes pero Pending no puede estabilizarse para recuperar sus IDs, la aplicación identifica la ejecución como incompleta y pide revisar Pending antes de reintentar. En el siguiente preflight, un conjunto completo puede reutilizarse para enviar esos IDs por correo sin volver a crear solicitudes.

La aplicación usa esperas adaptativas: comprueba inmediatamente cada estado y solo espera si PAM u Outlook aún están procesando. Pending se captura por página en una única operación DOM: Edge entrega juntas todas las filas y celdas visibles y C# valida después columnas, fechas, estados e IDs. Se evitan así las lecturas secuenciales de cientos de celdas sin relajar la exigencia de snapshots completos y estables. Tanto el preflight como la reconciliación posterior comparten la recuperación de lectura: ante un timeout o snapshot inestable recarga esa vista y, si continúa fallando, cierra y reemplaza únicamente la sesión autenticada de lectura. Admite como máximo dos lecturas por sesión y dos sesiones, sujeto al presupuesto global de 120 segundos; una página cerrada por vencimiento exige pasar directamente a otra sesión. Conserva la línea base original durante la reconciliación. Cancelaciones, filas persistentemente inválidas, IDs duplicados, ambigüedades y errores no clasificados no reconstruyen sesiones. Un fallo de autenticación termina sin repetir el login. Solo la reconstrucción necesaria por un fallo de lectura puede repetir login/TOTP; `Submit Request` y `Send()` nunca se repiten automáticamente.

### Publicar una nueva versión portable

1. Actualizar `Version`, `AssemblyVersion`, `FileVersion` e `InformationalVersion` en `PamAutomation.App/PamAutomation.App.csproj`. Para `1.0.2`, las versiones de ensamblado y archivo son `1.0.2.0`.
2. Desde la raíz, ejecutar el automatizador local excluido de Git. Este restaura, compila, ejecuta la suite ordinaria, publica, crea el ZIP y verifica los metadatos y el SHA-256:

   ```powershell
   .\Publish-Portable.ps1
   ```

   Si la salida de esa misma versión ya existe y se desea regenerarla deliberadamente:

   ```powershell
   .\Publish-Portable.ps1 -ReplaceExisting
   ```

3. Recoger `artifacts\publish\final\PAM-Automation-<versión>-win-x64.zip`; la carpeta contigua es únicamente la salida sin comprimir usada para validar y empaquetar.
4. En GitHub, crear el tag y release `v<versión>`, adjuntar el ZIP como asset y publicar el release. No se debe subir la carpeta ni sus archivos individuales al repositorio.
5. Solo después de publicar el asset, actualizar `latest.json` en `main` con la versión nueva, la fecha ISO y `repositoryUrl` igual a `https://github.com/OmarTellez1/Pam-automation-installer`. Este orden evita anunciar una versión que todavía no se puede descargar.

El usuario final debe descargar el ZIP, extraer la carpeta completa y ejecutar `PAM Automation.exe` desde esa carpeta. No debe ejecutar el programa dentro del visor del ZIP ni mover solamente el `.exe`, porque las bibliotecas contiguas forman parte de la publicación autocontenida.

La prueba del aviso de Pending sin correo ya comprueba la instrucción vigente `activa «Enviar correo con Outlook»`, además del bloqueo de reutilización. La suite ordinaria de publicación termina con 505 pruebas aprobadas, siete omitidas y cero fallos (512 en total). Las siete omitidas requieren habilitar explícitamente el navegador local; no bloquean la publicación. La advertencia heredada MSTEST0032 tampoco la bloquea. El paquete verificado está en `artifacts/publish/final/PAM-Automation-1.0.2-win-x64.zip`; para regenerar esa misma salida existente se utiliza `-ReplaceExisting`.

La aplicación seguirá requiriendo deliberadamente Windows, Microsoft Edge instalado, conectividad al portal PAM y Outlook clásico instalado cuando se active el correo. Edge y Outlook no se incluirán dentro del ejecutable porque deben utilizar las instalaciones corporativas existentes.

La integración con Outlook no depende de una ruta de Office. Intenta reutilizar la instancia clásica abierta y, si Windows informa `0x80080005`, abre una vez la aplicación registrada, espera su inicialización y reintenta COM. PAM Automation y Outlook deben ejecutarse en la misma sesión de Windows y con el mismo nivel de permisos. Después de entregar un correo a Outlook, la aplicación concede hasta 40 segundos para observar su paso a Enviados y tolera lecturas COM transitorias; nunca mueve por sí misma mensajes de Bandeja de salida ni repite un envío incierto.

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

## Corrección de lectura de Pending del 22 de septiembre

Pending reconoce tanto `30 items` como `1-25 of 30 items`. Un contador ausente nunca autoriza una lectura completa. El tamaño máximo se confirma mediante filas, rango, paginación y Status habilitado después de cargar, sin volver a localizar el selector mientras PAM lo retira del DOM. Antes de avanzar se comprueban nuevamente la página y el botón habilitado.

Los controles disponen de cinco segundos, cada lectura de 30 segundos y la recuperación de una etapa de un presupuesto compartido de 120 segundos, incluida autenticación (más cierre de recursos). Una lectura vencida cierra su página y obliga a reemplazar la sesión para evitar acciones pendientes sobre una vista recargada. Los límites de dos lecturas por sesión y dos sesiones son máximos: el presupuesto global puede terminar antes.

El diagnóstico añade duración por lectura y, ante fallos, operación, página, total publicado, filas observadas y estado del botón siguiente. Continúa únicamente en memoria hasta una exportación explícita. La integración autorizada leyó 30 filas tres veces consecutivas en 6,1, 6,1 y 9,0 segundos, sin crear solicitudes ni usar Outlook. La aceptación con un lote nuevo queda para la siguiente ejecución operativa del usuario.

## Próximo paso

El selector de acumulación aparece a la derecha de `Mostrar IDs al final` / `Enviar correo con Outlook`, con el mismo estilo redondeado y una etiqueta en dos líneas. El contador y las acciones manuales permanecen debajo.

El control ajusta su altura para mostrar toda la etiqueta. Mientras la acumulación está activa, el selector de Outlook deshabilitado aparece gris.

La **fase 27 está implementada** en el código: activa `Acumular solicitudes para un solo correo` para sumar los Request ID nuevos de ejecuciones completas de la misma colección (8 + 6 = 14, sin duplicados). Mientras esté activo, las acciones finales automáticas quedan deshabilitadas y se conserva su preferencia anterior. El contador, `Enviar correo` y `Descartar acumulado` aparecen junto al modo.

Con IDs pendientes no puedes cambiar/desactivar/eliminar la colección vinculada ni salir del modo. Actualizar Favorites tampoco cambia su selección: si queda vacía, puedes corregir su correo y enviar los IDs, pero no iniciar solicitudes nuevas. Puedes ajustar inicio/duración/motivo para otra ejecución; duplicados exactos en Pending bloquean ese intento y no agregan IDs antiguos. Fuera del modo se conserva el comportamiento habitual.

`Enviar correo` valida el correo actual de esa colección, remitente principal y firma común, y pide una sola confirmación. Confirmar Enviados vacía el acumulado. Ante un envío incierto se conservan los IDs y el botón pasa a `Comprobar envío`, sin reenviar. Descartar requiere confirmación y no cancela solicitudes PAM ni elimina correos. Al cerrar normalmente se advierte la pérdida de IDs: solo viven en memoria y no se recuperan tras un cierre inesperado.

Validación de fase 27: compilación `Release | x64` y 119 pruebas relacionadas aprobadas, incluidas pruebas WPF y dobles de PAM/Outlook. Advertencia heredada `MSTEST0032`, sin errores de compilación. No se crearon solicitudes ni se enviaron correos reales. Sin cambio de versión ni publicación; el ejecutable publicado anterior no incorpora esta fase. Comandos y límites en `Fases.md`.

La copia local más reciente está en `artifacts/publish/copy-request-ids-20260922/PAM Automation.exe`, Windows x64 autocontenida con `InformationalVersion=1.0.2-copy-request-ids.20260922`. Incluye las mejoras anteriores de Pending y Outlook. Debe conservarse la carpeta completa con sus dependencias. No se publica un release externo ni se modifica la versión estable del proyecto.

La ventana de resultados con IDs incorpora `Copiar Request ID`, que copia únicamente los identificadores, uno por línea y en el orden mostrado. Confirma la copia dentro de la misma ventana y permite reintentar si el portapapeles está ocupado. La lista es seleccionable y desplazable; `Aceptar` o Escape cierra el resultado. La compilación Release x64, 36 pruebas de la ventana principal y la comprobación visual WPF con 12/250 IDs aprobaron. La copia y el portapapeles ocupado se comprobaron con un destino simulado, sin alterar el portapapeles del usuario.

Pending vacío ya no intenta cambiar el tamaño de página: omite ese control únicamente después de confirmar total cero, cero filas y una vista estable, y vuelve a validar durante el escaneo. El contador desconocido y la carga en curso siguen sin aceptarse como vacíos. La compilación Release x64 y 66 pruebas focalizadas aprobaron; la integración autorizada con Edge visible leyó Pending vacío tres veces en 4,27, 4,05 y 4,13 segundos. No se crearon ni modificaron solicitudes ni se utilizó Outlook durante esta validación.

Outlook filtra candidatos por asunto y valida sus Request ID exactos. Después del envío consulta Enviados; Bandeja de salida y Borradores se inspeccionan solo al final de la ventana si todavía falta confirmar. Los fallos de lectura reconocidos se recuperan sin repetir `Send()` y sin duplicar `ERROR`/`WARNING` por el mismo incidente. Los errores permanentes detienen la observación y conservan el estado de envío aceptado pero no confirmado.

Validación local: compilación Release x64 y 77 pruebas focalizadas aprobadas, sin abrir Outlook ni enviar correos. Permanece la advertencia heredada MSTEST0032. Una selección inicial por nombre incluyó además el fallo ya conocido del texto de `RepeatedPendingDecisionWindow_CompleteSetWithoutEmail_RequiresOutlook`, ajeno a esta corrección. La próxima ejecución operativa y su diagnóstico permitirán validar la mejora frente al Outlook real.

Completar la fase 19 como aceptación final del ejecutable en otro equipo Windows administrado. Cualquier prueba real contra PAM requerirá autorización expresa.

## Estabilización del 1 de octubre de 2026

El calendario conserva subcausas tipadas, etapa, conteos y las últimas 20 transiciones en memoria. No se aumentaron sus límites de 8/45 segundos. La verificación de formulario exige valores completos durante al menos 600 ms, reinicia la racha ante reemplazos/errores y admite una sola corrección por formulario compartida entre AfterFill y BeforeSubmit. No repite Submit.

Un lote interrumpido conserva todos sus elementos y separa aceptación por respuesta, conflicto, rechazo inequívoco, fallo antes del envío, envío incierto y elemento no intentado. Recupera IDs nuevos consultando Pending con la línea base original. Solo verifica grupos completos e inequívocos; los grupos repetidos incompletos no asignan IDs por orden. El resultado incompleto permite revisar/copiar los IDs recuperados, conserva el acumulado anterior y bloquea correo/incorporación automática del lote.

El diagnóstico distingue el último intento real del último resultado completado, correlaciona etapas/ordinal/conteos y conserva la etapa inicial del fallo durante la recuperación. Incluye versiones/MVID de componentes cargados y versiones efectivas de Edge/Playwright cuando están disponibles. Los logs permanecen acotados en memoria; la exportación de producción continúa siendo explícita. Favorites se conserva y el margen temporal sigue diferido. Evidencia y límites actuales: [problemas.md](problemas.md), sección 11.
