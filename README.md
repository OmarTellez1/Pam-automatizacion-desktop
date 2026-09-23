# Pam Automation .NET

Aplicación de escritorio Windows para automatizar BeyondInsight Password Safe desde una solución construida desde cero exclusivamente con C# y .NET.

## Estado actual

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation`.

La solución `PamAutomation.slnx` contiene la aplicación WPF `PamAutomation.App` y los proyectos base `PamAutomation.Core`, `PamAutomation.Browser`, `PamAutomation.Outlook`, `PamAutomation.Infrastructure` y `PamAutomation.Tests`. Las fases 3A y 3B incorporan el dominio, la validación y la persistencia JSON de la configuración no sensible; las fases 4 a 7 incorporan credenciales protegidas, TOTP, reloj HTTP y sesión restringida de Edge. Las fases 8 a 13 agregan autenticación simulada, login real, lectura paginada de Favorites, formularios seguros, solicitudes confirmadas y lectura/comparación de Pending; la fase 14 incorpora Outlook clásico, la fase 15 la composición HTML saneada, la fase 16 conecta el flujo PAM con la interfaz, la fase 17 permite exportar diagnóstico seguro y la fase 18 produce la aplicación portable versionada. Las fases 21–21.10 añaden acceso interactivo y robustez operativa. Las fases 20.1–20.5 incorporan el catálogo protegido, colecciones editables y eliminables, `All favorites` sincronizada y la colección activa como único alcance de automatización. La fase 22 optimiza el arranque; la fase 23 completa el usuario principal obligatorio, la administración protegida de cuentas PAM secundarias, su selección exclusiva para el acceso interactivo y la aceptación de migración y seguridad.

La fase 24 hace genérica la reconciliación de Pending: conserva la advertencia previa de duplicados, excluye los Request ID existentes antes del envío y asocia únicamente filas nuevas mediante `System + Account`, inicio, expiración, descripción y cantidades. Retiró las familias hardcodeadas de `Application` para admitir cualquiera de los aproximadamente 383 accesos disponibles sin asumir que todos se solicitan en una ejecución. Una segunda barrera impide que un ID de la línea base llegue accidentalmente al resultado o al correo. Cuando el usuario elige deliberadamente reutilizar solicitudes existentes, la aplicación exige un conjunto completo y no ambiguo, omite la creación PAM y envía exclusivamente esos IDs mediante Outlook.

La lectura de Pending selecciona primero la mayor cantidad de filas que ofrece el portal —actualmente 200— para obtener lotes habituales desde una sola página. Espera a que `Status = Pending` termine de cargar, comprueba el total publicado y conserva el recorrido paginado como respaldo cuando el total supera el máximo o el portal no expone el selector.

El diagnóstico explícito incorpora `pending-reconciliation.json` desde el inicio del preflight de Pending, incluso si nunca llega a obtener una línea base. La traza de esquema 3 distingue `Preflight` y `Reconciliation`, registra cada lectura y recuperación por sesión y conserva el error final. La línea base ausente se omite del JSON; una lectura vacía válida sí aparece como snapshot de cero filas. El archivo conserva en memoria y exporta únicamente por decisión del usuario Request ID, `System`, `Application`, `Account`, inicio, expiración, descripción, diferencias y multiplicidades. Credenciales, contraseña, semilla y código TOTP, cookies, tokens y contenido del correo permanecen excluidos.

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

La copia local más reciente está en `artifacts/publish/copy-request-ids-20260922/PAM Automation.exe`, Windows x64 autocontenida con `InformationalVersion=1.0.2-copy-request-ids.20260922`. Incluye las mejoras anteriores de Pending y Outlook. Debe conservarse la carpeta completa con sus dependencias. No se publica un release externo ni se modifica la versión estable del proyecto.

La ventana de resultados con IDs incorpora `Copiar Request ID`, que copia únicamente los identificadores, uno por línea y en el orden mostrado. Confirma la copia dentro de la misma ventana y permite reintentar si el portapapeles está ocupado. La lista es seleccionable y desplazable; `Aceptar` o Escape cierra el resultado. La compilación Release x64, 36 pruebas de la ventana principal y la comprobación visual WPF con 12/250 IDs aprobaron. La copia y el portapapeles ocupado se comprobaron con un destino simulado, sin alterar el portapapeles del usuario.

Pending vacío ya no intenta cambiar el tamaño de página: omite ese control únicamente después de confirmar total cero, cero filas y una vista estable, y vuelve a validar durante el escaneo. El contador desconocido y la carga en curso siguen sin aceptarse como vacíos. La compilación Release x64 y 66 pruebas focalizadas aprobaron; la integración autorizada con Edge visible leyó Pending vacío tres veces en 4,27, 4,05 y 4,13 segundos. No se crearon ni modificaron solicitudes ni se utilizó Outlook durante esta validación.

Outlook filtra candidatos por asunto y valida sus Request ID exactos. Después del envío consulta Enviados; Bandeja de salida y Borradores se inspeccionan solo al final de la ventana si todavía falta confirmar. Los fallos de lectura reconocidos se recuperan sin repetir `Send()` y sin duplicar `ERROR`/`WARNING` por el mismo incidente. Los errores permanentes detienen la observación y conservan el estado de envío aceptado pero no confirmado.

Validación local: compilación Release x64 y 77 pruebas focalizadas aprobadas, sin abrir Outlook ni enviar correos. Permanece la advertencia heredada MSTEST0032. Una selección inicial por nombre incluyó además el fallo ya conocido del texto de `RepeatedPendingDecisionWindow_CompleteSetWithoutEmail_RequiresOutlook`, ajeno a esta corrección. La próxima ejecución operativa y su diagnóstico permitirán validar la mejora frente al Outlook real.

Completar la fase 19 como aceptación final del ejecutable en otro equipo Windows administrado. Cualquier prueba real contra PAM requerirá autorización expresa.
