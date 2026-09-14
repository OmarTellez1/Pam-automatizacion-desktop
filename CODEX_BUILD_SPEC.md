> Documento canónico para C:\pam automatizacion dotnet\PamAutomation. La aplicación anterior en C:\pam automatizacion es únicamente referencia de lectura.

# Especificación para construir la aplicación con Codex

## Instrucción principal

Construir esta aplicación desde cero utilizando únicamente C# y .NET para la aplicación de escritorio, la lógica de negocio, la automatización del navegador, la integración con Outlook, la configuración, las pruebas y el empaquetado.

No utilizar JavaScript, TypeScript, Node.js, Electron, Python, Java, PowerShell, Selenium ni WebView2 como parte de la aplicación.

XAML está permitido únicamente como lenguaje declarativo de interfaz para WPF. La interfaz debe ser nativa de Windows y la lógica debe estar implementada en C#.

La aplicación debe ser autocontenida respecto de .NET y entregarse como un ejecutable portable Windows x64. El usuario lo guardará en una ubicación donde tenga permisos y eliminará manualmente una versión anterior después de descargar la nueva. No habrá MSI/MSIX en esta primera distribución. El ejecutable debe incluir toda la lógica necesaria para configuración, TOTP, Playwright, solicitudes PAM, resultados en memoria y Outlook COM. No debe exigir instalar .NET, Node.js, Python ni PowerShell en el equipo final.

La aplicación utilizará deliberadamente dependencias externas instaladas por la empresa: Windows, Microsoft Edge, conectividad al portal PAM y Outlook clásico cuando se habilite el correo. Edge y Outlook no se incluirán dentro del ejecutable.

No incluir Chromium ni descargar navegadores durante la ejecución.

## Objetivo del proyecto

Crear una aplicación de escritorio Windows para automatizar de forma controlada el acceso a BeyondInsight Password Safe en `https://10.30.17.117`.

La aplicación debe:

1. Permitir configurar las credenciales PAM.
2. Generar códigos TOTP a partir de una clave Base32 de Microsoft Authenticator.
3. Iniciar sesión en BeyondInsight mediante Playwright .NET.
4. Utilizar Microsoft Edge instalado localmente como navegador automatizado.
5. Entrar en `Accounts > Browse By Category > Favorites`.
6. Crear o recuperar solicitudes PAM para las `Favorites` rows seleccionadas, identificadas por `System + Application + Account`.
7. Ejecutar pruebas sin crear solicitudes reales.
8. Crear solicitudes reales únicamente después de confirmación explícita.
9. Leer y validar solicitudes en estado `Pending`.
10. Enviar opcionalmente el resultado mediante Outlook clásico.
11. Mostrar los Request ID y los conflictos en la interfaz.
12. Mantener resultados en memoria sin crear archivos operativos persistentes.
13. Funcionar como un ejecutable Windows autocontenido.

La aplicación será de uso interno, exclusivamente para Windows y con conectividad a la red o VPN donde se encuentra el portal PAM.

## Decisión tecnológica obligatoria

### Plataforma

- C#.
- .NET 10 LTS, salvo que el entorno corporativo obligue a utilizar otra versión soportada.
- WPF para la interfaz nativa de escritorio.
- XAML para definir layout, estilos, plantillas y controles visuales.
- Arquitectura MVVM, preferiblemente con `CommunityToolkit.Mvvm`.

### Automatización de navegador

- `Microsoft.Playwright` para .NET.
- Utilizar la opción 1: el canal instalado de Microsoft Edge:

```csharp
Channel = "msedge"
```

- Edge es un requisito de instalación de la aplicación.
- Chrome puede añadirse posteriormente como compatibilidad opcional, pero no debe ser necesario para la primera versión.
- No incluir navegadores Playwright en la publicación.
- No descargar navegadores durante la ejecución.
- No utilizar coordenadas de pantalla.
- No utilizar automatización visual.
- Crear un contexto temporal de Playwright después de lanzar Edge.
- No seleccionar, reutilizar ni modificar perfiles existentes, cookies, extensiones o sesiones del usuario.
- No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente.
- El aislamiento es interno de Playwright y no debe presentarse al usuario como una ventana de incógnito.
- Preferir un contexto Playwright aislado por ejecución.
- Cerrar siempre browser, context y page mediante `await using` o bloques equivalentes.

Playwright .NET debe controlar el Edge instalado externamente. La aplicación debe comprobar al inicio que Edge existe y mostrar un mensaje claro si no está disponible.

La primera versión ejecutará Edge de forma visible para la comprobación de credenciales y las automatizaciones reales. Se conservarán las reglas funcionales y validaciones comprobadas en la aplicación anterior, pero no su uso de Chrome, perfiles existentes o ejecución headless. Un modo headless futuro requerirá autorización explícita, una decisión documentada y pruebas de seguridad e integración equivalentes.

## Interfaz de usuario

La interfaz debe ser equivalente en calidad visual a la aplicación actual, pero implementada de forma nativa con WPF y XAML.

Debe incluir:

- Ventana principal limpia y centrada.
- Acción principal azul y visualmente destacada para iniciar la automatización.
- Acción de prueba separada y visible únicamente cuando esté habilitada.
- Botones secundarios de configuración.
- Estados visuales para configuración completa o incompleta.
- Indicador de progreso o spinner durante una ejecución.
- Indicador de éxito o error al finalizar.
- Diálogos modales para confirmaciones, resultados y errores.
- Desactivación de todos los controles mientras una automatización está activa.
- Soporte de escalado DPI y redimensionamiento razonable.
- Colores, espaciado, radios, tipografía e iconos centralizados en recursos XAML.
- Accesibilidad básica mediante nombres, navegación por teclado y contraste suficiente.

La interfaz no debe contener JavaScript ni HTML para funcionar. El HTML únicamente puede existir como contenido de correo generado por C#.

### Pantallas principales

1. Inicio.
2. Configuración de credenciales PAM.
3. Configuración de cuenta Outlook.
4. Configuración de solicitud.
5. Configuración de correo.
6. Resultado de la automatización.
7. Diálogo de confirmación para el modo real.

### Controles de configuración

La pantalla inicial debe mostrar:

- `Configurar credenciales` o `Editar credenciales`.
- `Configurar solicitud` o `Editar solicitud`.
- `Configurar envío de correo` o `Editar envío de correo`.
- Toggle entre `Mostrar IDs al final` y `Enviar correo al final`.
- `Iniciar prueba`, solo cuando la configuración lo permita.
- `Iniciar automatización` como acción principal.

La aplicación debe bloquear un segundo inicio mientras exista una automatización activa.

El botón `Iniciar prueba` estará gobernado por la propiedad no sensible `testModeEnabled` de `app-settings.json`, cuyo valor predeterminado será `false`. El usuario podrá cambiarla mediante `Habilitar modo de prueba` dentro de una sección avanzada de Configuración de solicitud, sin editar manualmente el archivo JSON. El botón solo será visible cuando esa propiedad sea verdadera y las configuraciones de credenciales y solicitud sean válidas. Si la acción final es correo, también se exigirá una configuración de correo válida.

## Credenciales y seguridad

No utilizar `.env` para guardar secretos en la versión C#.

No utilizar archivos `.env` para ninguna configuración de la aplicación nueva. El reemplazo será:

    %LOCALAPPDATA%\PamAutomation\config\app-settings.json
    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat


Las credenciales PAM se almacenarán en Windows Credential Manager, asociadas al usuario actual de Windows:

- Usuario PAM.
- Contraseña PAM.
- Semilla TOTP de Microsoft Authenticator.

La configuración general se guardará en `app-settings.json` con `schemaVersion` e incluirá:

- `finalAction`, equivalente a `PAM_FINAL_ACTION`.
- `requestReason`, equivalente a `PAM_REQUEST_REASON`.
- `sessionHours`, equivalente a `PAM_SESSION_HOURS`.
- `startDate`, equivalente a `PAM_START_DATE`, persistida como fecha ISO.
- `startTime`, equivalente a `PAM_START_TIME`.
- `timeZone`, con valor `America/Guayaquil`.
- `testModeEnabled`, con valor predeterminado `false`.

La configuración de correo se guardará en `mail-settings.dat` y se protegerá con DPAPI `CurrentUser`. Contendrá remitente, destinatarios Para, destinatarios CC, asunto y HTML enriquecido con el marcador `{{REQUEST_IDS}}`.

No crear ni mantener carpetas persistentes de `results`, `logs`, `artifacts` o `temp` en `%PROGRAMDATA%`, `%LOCALAPPDATA%` ni junto al ejecutable. Los resultados de la ejecución deben mantenerse en memoria y mostrarse en la interfaz mientras la aplicación esté abierta.

Si la acción final es correo, el mensaje debe prepararse, validarse y enviarse dentro de la misma ejecución. No utilizar `last-run`, `last-email` ni `delivery-state` persistentes. La protección inmediata contra duplicados se mantendrá en memoria y, antes del envío, se consultarán Enviados, Bandeja de salida y Borradores de Outlook buscando el mismo asunto y el mismo conjunto exacto de Request ID. Outlook actúa como fuente externa de confirmación; la aplicación no crea historial local. Si la aplicación se cierra inesperadamente, no se requiere recuperación local.

No habrá auditoría local ni debugging persistente en el uso normal. Un diagnóstico futuro deberá activarse explícitamente y exportarse manualmente a una ruta elegida por soporte o desarrollo.

Esta regla aplica al ejecutable publicado y utilizado por los compañeros. El repositorio de desarrollo sí debe permitir evidencias técnicas para investigar errores. Durante desarrollo, pruebas e integración controlada, se podrán crear únicamente en la raíz del repositorio:

```text
results\
logs\
artifacts\
temp\
```

Estas carpetas deben estar excluidas de Git, no deben copiarse al instalador ni distribuirse con el ejecutable. Solo pueden contener resultados y diagnóstico técnico sin secretos. No guardar contraseñas, semilla TOTP, código TOTP, cookies, tokens ni perfiles de navegador. El código debe distinguir el perfil de desarrollo del perfil publicado para evitar que el ejecutable instalado escriba en esas carpetas.

Todas las escrituras deben ser atómicas. Las configuraciones deben poder migrarse mediante `schemaVersion`. No escribir archivos de usuario dentro de `Program Files` ni junto al ejecutable.

Guardar la información sensible mediante Windows Credential Manager o DPAPI (`ProtectedData`). La contraseña PAM y la semilla TOTP deben almacenarse cifradas y nunca deben aparecer en:

- Logs.
- Excepciones mostradas al usuario.
- Capturas de pantalla.
- Resultados JSON.
- Mensajes de diagnóstico.
- Código fuente.
- Commits.

La aplicación debe solicitar:

- Usuario PAM.
- Contraseña PAM.
- Clave Base32 de enrolamiento de Microsoft Authenticator.
- Cuenta remitente de Outlook.

La clave Base32 no es el código temporal de seis dígitos. La interfaz debe explicarlo claramente.

Durante una edición de credenciales, los campos vacíos deben conservar el valor existente. Nunca devolver los secretos guardados a la interfaz en texto plano.

## Restricción de red

La automatización solo puede interactuar con:

```text
https://10.30.17.117
```

El código debe validar el origen de todas las URLs de navegación y bloquear cualquier origen distinto.

La excepción de certificado HTTPS debe limitarse al contexto Playwright de PAM. No desactivar la validación TLS global del proceso.

No guardar cookies ni sesiones persistentes de PAM salvo que exista una decisión de seguridad documentada.

## Autenticación y TOTP

La autenticación debe implementarse como un servicio aislado y testeable.

Requisitos:

- Implementar RFC 6238 con HMAC-SHA1 y códigos de seis dígitos.
- Usar la hora HTTP del portal PAM obtenida desde el encabezado `Date`.
- No compensar el desfase modificando el reloj de Windows.
- No volver silenciosamente al reloj local si el portal no entrega una hora válida.
- Detener la autenticación con un error específico cuando no exista una hora HTTP válida.
- Esperar una ventana segura antes de introducir el TOTP.
- Exigir al menos ocho segundos de vigencia antes del envío.
- Si PAM rechaza explícitamente el código, esperar el siguiente intervalo y reintentar una sola vez.
- No reintentar automáticamente fallos de conexión, timeouts o errores de confirmación.
- Esperar que desaparezca el mensaje de rechazo anterior sin utilizar estados de Playwright inválidos.
- Confirmar el login esperando que aparezca `Return to dashboard`.

La autenticación debe diferenciar:

1. Rechazo de usuario o contraseña.
2. Rechazo del TOTP.
3. Fallo de conexión o certificado.
4. Falta de confirmación final del portal.

Nunca escribir el código TOTP en consola o logs.

## Configuración de solicitudes

Persistir:

- Fecha de inicio.
- Hora de inicio.
- Duración entre 1 y 8 horas.
- Razón del acceso.

Reglas:

- Utilizar la zona horaria `America/Guayaquil`.
- La fecha se muestra con año vigente y lectura completa.
- No permitir fechas anteriores al día actual de Ecuador.
- No permitir días inexistentes de cada mes.
- No permitir horas pasadas antes de iniciar la automatización.
- Mantener internamente una representación temporal inequívoca y comprobar el valor de 24 horas normalizado por PAM.
- Mostrar y solicitar la hora al usuario en formato de 12 horas con selector explícito `AM/PM`.
- Mantener `Days` en cero.
- Escribir la duración únicamente en `Hours`.
- Validar fecha, hora, duración y razón antes de comenzar.

Antes de cada `Submit Request`:

- Esperar que el formulario termine de inicializarse.
- Exigir que `Application Session` permanezca seleccionado.
- Comprobar fecha dos veces.
- Comprobar hora dos veces.
- Comprobar días, horas y minutos.
- Comprobar la razón.
- Verificar que el calendario conserve la fecha durante un intervalo estable.
- Bloquear el envío si PAM restablece algún valor.

Si alguno de estos controles falla, no pulsar `Submit Request`.

## Modo de prueba

El modo de prueba debe:

- Entrar en Favorites.
- Recorrer todas las `Favorites` rows encontradas.
- Llenar cada formulario.
- Validar los campos.
- Descartar el formulario.
- Nunca pulsar `Submit Request`.
- Finalizar con `submitted = 0`.

Cuando la acción final sea correo, el envío del resultado de prueba será una acción manual permitida con estas restricciones obligatorias:

- Enviar únicamente a `tellezoe@hitss.com`.
- No utilizar otros destinatarios configurados y dejar CC vacío.
- Prefijar el asunto con `[PRUEBA]`.
- Mostrar el destinatario exacto y solicitar confirmación explícita inmediatamente antes del envío.
- Mantener prohibido el envío desde pruebas unitarias o integraciones automatizadas.

La lista simulada autorizada debe ser exactamente:

```text
137288, 137287, 137286, 137284, 137283, 137282, 137281,
137280, 137278, 137276, 137275, 137274, 137273, 137271
```

La lista debe mantenerse en una única constante del dominio de pruebas y nunca mezclarse con la lógica real.

## Modo real

El modo real debe:

- Requerir confirmación explícita antes de iniciar.
- Entrar en Favorites.
- Contar las cuentas dinámicamente.
- Recorrer todas las páginas.
- Crear o recuperar una solicitud `Pending` por cada cuenta.
- Comparar `System`, `Application`, `Account` y familia de `Application`.
- Comparar exactamente fecha y hora de inicio.
- No asumir que existen 14 cuentas.
- No asumir que todo cabe en una página.
- No consultar ni abrir el estado `Approved`.

Si PAM rechaza una cuenta con el código estable:

```text
PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable
```

registrarla como conflicto y continuar con la siguiente cuenta.

Otros errores deben detener el proceso y conservar la evidencia.

El resultado debe incluir:

- `favoriteCount`.
- Request ID obtenidos.
- Cuentas procesadas.
- Conflictos.
- Errores.
- Fecha y hora de la ejecución.
- Acción final seleccionada.

## Outlook clásico

La aplicación debe utilizar exclusivamente Outlook clásico mediante Microsoft Outlook Interop/COM.

No utilizar:

- Nuevo Outlook.
- SMTP directo.
- Microsoft Graph.
- Automatización visual.
- Clics por coordenadas.

Requisitos:

- Outlook clásico instalado.
- Perfil configurado.
- Sesión de Outlook disponible.
- Remitente configurado exactamente por `SmtpAddress`.
- Si no existe exactamente una coincidencia, detener el envío.

Antes de guardar el remitente, crear un mensaje temporal, asignar la cuenta y descartarlo. Esta comprobación no debe enviar correo.

Después de configurar la cuenta, la interfaz podrá ofrecer una acción manual `Enviar correo de prueba` para verificar Outlook de extremo a extremo. Este mensaje se enviará exclusivamente a la misma dirección SMTP configurada como remitente, sin CC, con una identificación clara de prueba y después de una confirmación explícita. Esta verificación de Outlook es distinta del correo generado por el modo de prueba PAM, cuyo destinatario continúa limitado a `tellezoe@hitss.com`. Ninguna de las dos acciones se ejecutará desde pruebas unitarias o automatizadas.

Para enviar:

- Crear siempre un mensaje nuevo.
- Asignar `SendUsingAccount` antes de `Send()`.
- No reutilizar borradores antiguos.
- Validar destinatarios y copias.
- Evitar duplicados entre Para y CC.
- Confirmar que todos los Request ID esperados aparecen en el HTML.
- Comprobar el asunto y la cuenta de envío.
- Esperar confirmación en Enviados cuando sea posible.
- Si el correo queda en Bandeja de salida, moverlo a Borradores y marcar el resultado como no confirmado.

La automatización COM debe ejecutarse en un contexto STA y liberar referencias COM de forma explícita. No retener objetos COM más tiempo del necesario. Evitar crear cadenas largas de llamadas sobre objetos COM temporales.

## Contenido del correo

El contenido debe incluir exactamente una vez:

```text
{{REQUEST_IDS}}
```

Al preparar el correo, reemplazar el marcador por los Request ID actuales, uno por línea.

Conservar el contenido enriquecido configurado por el usuario, incluyendo tablas y motivos de acceso.

Como no se permite JavaScript ni WebView2, la edición enriquecida se resolverá con `RichTextBox` y `FlowDocument` como modelo nativo. Un convertidor propio escrito en C# recorrerá el documento y emitirá únicamente un subconjunto permitido y saneado de HTML para Outlook. No se admitirá entrada de HTML arbitrario. Validar primero un prototipo de:

- Texto con formato.
- Tablas.
- Imágenes pegadas.
- Conversión a HTML.
- Imágenes inline con Content-ID.
- Saneamiento del HTML.
- Persistencia y recuperación del documento y sus imágenes dentro de `mail-settings.dat` protegido con DPAPI.

El subconjunto inicial incluirá párrafos, negrita, cursiva, subrayado, listas, tablas, imágenes pegadas y `{{REQUEST_IDS}}`. No fingir que un `RichTextBox` convierte HTML completo sin limitaciones. Si la conversión no conserva correctamente tablas o imágenes, detenerse y documentar una decisión técnica antes de continuar. Los archivos temporales necesarios para adjuntos CID deben eliminarse al finalizar la operación.

El HTML final no puede contener:

- Scripts.
- Objetos.
- Marcos.
- Conexiones externas.
- Recursos remotos no autorizados.

## Arquitectura de la solución

Crear una solución similar a:

```text
PamAutomation.slnx
├── PamAutomation.App
│   ├── WPF
│   ├── Views
│   ├── ViewModels
│   └── Resources
├── PamAutomation.Core
│   ├── Models
│   ├── Interfaces
│   ├── Validation
│   ├── TOTP
│   └── BusinessRules
├── PamAutomation.Browser
│   ├── PamBrowserSession
│   ├── Authentication
│   ├── Favorites
│   └── Requests
├── PamAutomation.Outlook
│   ├── OutlookClient
│   ├── AccountResolver
│   └── MailComposer
├── PamAutomation.Infrastructure
│   ├── CredentialStore
│   ├── FileStore
│   ├── Logging
│   ├── Clock
│   └── Configuration
└── PamAutomation.Tests
```

La lógica de negocio no debe vivir directamente en code-behind de las ventanas WPF.

Usar interfaces para aislar:

- Reloj.
- Almacenamiento de credenciales.
- Automatización del navegador.
- Outlook.
- Sistema de archivos.
- Fecha y hora de Ecuador.

Esto permitirá probar la lógica sin abrir Edge, PAM u Outlook.

## Dependencias recomendadas

Dependencias principales:

- `Microsoft.Playwright`.
- `CommunityToolkit.Mvvm`.
- `HtmlAgilityPack` exclusivamente para interpretar HTML real y potencialmente malformado del portapapeles antes del saneamiento estricto de firmas.
- `Microsoft.Extensions.DependencyInjection`.
- `Microsoft.Extensions.Logging`.
- `Microsoft.Office.Interop.Outlook` o referencia COM oficial a Outlook Object Library.
- `System.Security.Cryptography.ProtectedData` si se utiliza DPAPI mediante paquete.
- MSTest con Microsoft Testing Platform y su runner integrado para pruebas desde la terminal.

Usar primero las APIs incluidas en .NET:

- `System.Text.Json`.
- `System.Net.Http`.
- `System.Security.Cryptography`.
- `System.IO`.
- `System.Threading.Channels` si se necesita coordinar eventos.

No agregar paquetes grandes sin justificar su necesidad.

## Pruebas obligatorias

### Unitarias

Probar como mínimo:

- RFC 6238.
- Claves Base32 inválidas.
- Desfase de reloj.
- Ventana segura del TOTP.
- Límite de un solo reintento.
- Validación de fecha y hora.
- Meses y días reales.
- Duración de 1 a 8 horas.
- Direcciones de correo.
- Duplicados Para/CC.
- Marcador `REQUEST_IDS`.
- Saneamiento HTML.
- Fingerprints y detección de duplicados.
- Paginación de solicitudes.
- Conflictos PAM.
- Estados de ejecución.

### Pruebas de navegador

Crear un portal simulado local o una capa mock para probar:

- Login correcto.
- Usuario o contraseña rechazados.
- TOTP rechazado y segundo intento correcto.
- Dos TOTP rechazados.
- Portal sin encabezado `Date`.
- Navegación a origen no permitido.
- Formulario que restablece campos.
- Calendarización inestable.
- Solicitudes paginadas.
- Conflicto de acceso existente.

Las pruebas unitarias y simulaciones no deben usar credenciales reales ni conectarse a PAM. Las pruebas de integración visibles pueden utilizar credenciales reales desde el almacén protegido y conectarse al entorno PAM autorizado cuando el usuario lo confirme explícitamente. Incluso en esas pruebas no se crearán solicitudes reales ni se enviarán correos salvo autorización independiente y explícita.

### Pruebas de Outlook

Separar pruebas unitarias de pruebas COM reales.

Las pruebas unitarias deben utilizar un `FakeOutlookClient`.

Las pruebas COM reales deben:

- Ejecutarse solo en un equipo con Outlook clásico.
- Crear mensajes de prueba claramente identificados.
- No enviar por defecto.
- Requerir una confirmación separada para envío real.

## Empaquetado

Publicar desde la terminal para Windows x64 como aplicación autocontenida multifichero mediante `dotnet publish`, con configuración `Release`, runtime `win-x64`, `self-contained = true` y trimming deshabilitado. Las dependencias permanecen preextraídas para reducir el trabajo anterior a la aparición de WPF. La restauración, compilación, ejecución, pruebas y publicación dependerán del SDK x64 de .NET 10 y de `dotnet` disponible en `PATH`. La excepción local documentada permite que `Publish-Portable.ps1` automatice exclusivamente este procedimiento de release.

No activar trimming ni Native AOT durante la primera versión porque COM, reflexión y algunas dependencias pueden presentar incompatibilidades.

La salida distribuible es `PAM-Automation-<versión>-win-x64.zip`. Debe contener una sola carpeta base con `PAM Automation.exe` y todas sus dependencias autocontenidas. El usuario elige dónde extraerla, conserva la carpeta completa y no requiere permisos de administrador. La configuración privada continúa bajo `%LOCALAPPDATA%\PamAutomation\config`; las credenciales permanecen en Windows Credential Manager y nunca se escriben junto al ejecutable.

La aplicación incorpora versión nativa de Windows y consulta al iniciar un manifiesto JSON público, pequeño y limitado. Si la versión publicada es superior, muestra un aviso descartable dentro de WPF. No descarga, sustituye ni elimina ejecutables: el usuario obtiene la nueva versión desde el repositorio compartido y elimina manualmente la anterior. Una falla de red, TLS, formato o disponibilidad nunca bloquea el inicio. La comparación usa versiones numéricas, no fechas de archivo.

El repositorio de actualizaciones debe ser accesible desde la red corporativa mediante HTTPS con una cadena válida. No se permite ignorar errores de certificado ni analizar la interfaz HTML de un proveedor como sustituto de un endpoint de archivo estable.

## Logs, artefactos y diagnóstico

No crear logs, resultados JSON, capturas, trazas ni artefactos persistentes durante el uso normal. Los resultados de ejecución deben permanecer en memoria y la interfaz debe mostrar los Request ID, conflictos, errores y estado del correo mientras la aplicación esté abierta.

Si desarrollo o soporte necesita información adicional, implementar en una fase posterior un modo de diagnóstico explícito. Ese modo debe requerir una acción clara del usuario, exportar manualmente a una ruta seleccionada y nunca activarse por defecto. No debe guardar contraseñas, semilla TOTP, código TOTP, cookies, tokens ni HTML sensible.

Windows o Playwright pueden crear temporales internos en `%TEMP%` durante la ejecución. No considerarlos almacenamiento persistente de la aplicación, no escribir secretos allí y limpiar lo creado por la aplicación cuando sea posible.

## Entorno de desarrollo por terminal

Instalar el SDK x64 de .NET 10 LTS y mantener `dotnet` disponible en `PATH`. El SDK incluye la compilación de WPF y los runtimes de desarrollo necesarios; no instalar runtimes independientes para construir la solución.

Verificar el entorno con:

```text
dotnet --info
dotnet --list-sdks
```

Desde la raíz del repositorio, el flujo obligatorio es:

```text
dotnet restore PamAutomation.slnx -p:Platform=x64
dotnet build PamAutomation.slnx -c Release -p:Platform=x64 --no-restore
dotnet run --project PamAutomation.Tests\PamAutomation.Tests.csproj -c Release -p:Platform=x64 --no-build
dotnet run --project PamAutomation.App\PamAutomation.App.csproj -c Release -p:Platform=x64 --no-build
```

Las pruebas usan el runner integrado de Microsoft Testing Platform. Mientras no se defina MTP en `global.json`, ejecutar el proyecto de pruebas con `dotnet run`; no utilizar `dotnet test`, porque .NET 10 puede seleccionar el destino VSTest incompatible con la configuración actual.

No se requieren .NET MAUI, WinUI, C++, ASP.NET, Azure, Node.js, Python, Java ni herramientas web para construir esta solución.

## Reglas obligatorias para Codex

1. La primera vez que un thread nuevo de Codex entra al proyecto sin contexto previo, el agente principal debe leer una vez la documentación completa exigida por `AGENTS.md`. Las nuevas tareas o prompts dentro del mismo thread no reinician esa obligación: debe consultar bajo demanda solo los documentos necesarios, y una edición realizada por el propio agente no obliga a releerlos por completo. Un subagente debe obedecer `AGENTS.md` y leer únicamente los archivos y secciones que el coordinador asigne en su paquete de contexto; no debe recorrer todo el repositorio ni todos los Markdown por defecto.
2. Mantener actualizados `README.md`, `LOGIC.md` y `History.md` después de cambios relevantes.
3. No crear archivos `.js`, `.mjs`, `.ts`, `.py`, `.ps1` ni proyectos Node.
4. No cambiar la decisión de utilizar Edge externo sin una autorización explícita.
5. No incluir Chromium en la publicación.
6. No usar WebView2 como sustituto de la interfaz WPF.
7. No usar Selenium.
8. No inventar selectores del portal: inspeccionar y centralizar los selectores.
9. Preferir roles, labels y atributos estables frente a selectores CSS frágiles.
10. No usar esperas fijas cuando pueda utilizarse una condición observable.
11. No introducir reintentos automáticos adicionales sin documentarlos.
12. No imprimir, registrar ni hardcodear credenciales reales. Las pruebas de integración autorizadas pueden utilizarlas desde el almacén protegido.
13. No cambiar el reloj del sistema.
14. No enviar correos durante pruebas automatizadas.
15. No crear solicitudes reales durante pruebas.
16. No afirmar que una tarea está terminada sin compilar, probar y revisar los resultados.
17. Ejecutar las validaciones relacionadas únicamente con el cambio realizado.
18. Mostrar claramente cualquier limitación que no haya podido comprobarse.
19. Ante una decisión ambigua que afecte seguridad, datos reales o envío de solicitudes, detenerse y solicitar confirmación.
20. Mantener cambios pequeños y revisables.

## Orden recomendado de construcción

1. Crear solución C#/.NET y compilar una ventana WPF vacía.
2. Crear estilos visuales y la ventana principal.
3. Implementar configuración no sensible.
4. Implementar Credential Manager o DPAPI.
5. Implementar TOTP y pruebas RFC 6238.
6. Implementar un `IClock` para la hora del portal.
7. Implementar lanzamiento de Edge con Playwright .NET.
8. Implementar autenticación PAM simulada.
9. Implementar autenticación real visible utilizando la opción 1: Edge instalado y contexto temporal de Playwright, sin seleccionar ni reutilizar perfiles existentes, y solo con autorización explícita.
10. Implementar navegación a Favorites.
11. Implementar modo de prueba sin Submit Request.
12. Implementar solicitudes reales detrás de confirmación.
13. Implementar lectura paginada de Pending.
14. Implementar Outlook falso y después Outlook COM real.
15. Implementar composición HTML y marcador de Request ID.
16. Implementar resultados en memoria y mensajes de error sin persistencia para publicación.
17. Implementar evidencias de desarrollo en `results`, `logs`, `artifacts` y `temp` de la raíz, excluidas de Git.
18. Implementar publicación portable autocontenida y aviso manual de nueva versión sin incluir evidencias de desarrollo.
19. Ejecutar pruebas finales en un equipo Windows administrado.
20. Después de completar la migración, implementar colecciones locales editables para ejecutar subconjuntos de Favorites.

No avanzar a la siguiente etapa si la etapa actual no compila y no tiene una prueba mínima.

La fase 20 es una ampliación posterior a la migración y no modifica el camino crítico de las fases 12 a 19. Debe mostrar todas las `Favorites` rows, indicar cuáles tienen `Account Status = Available`, permitir que el usuario seleccione cualquier subconjunto, nombrar y editar la colección y conservar fecha, hora, duración y razón predeterminadas. Podrá admitir valores específicos por fila después de confirmar su experiencia de edición.

Las colecciones guardarán identidades completas `System + Application + Account`, no índices o páginas del grid, y deberán reconciliarse con un snapshot vivo de todas las páginas de `Favorites` antes de editarse o ejecutarse. Su persistencia será privada, versionada, atómica y protegida con DPAPI `CurrentUser`. El recorrido web y las invariantes detalladas se mantienen en `PAM_NAVIGATION.md`.

## Criterio de finalización

El proyecto estará terminado únicamente cuando:

- Compile en Release para `win-x64`.
- Se publique como aplicación autocontenida.
- Inicie en un equipo sin .NET, Node.js, Python o PowerShell instalado.
- Contenga dentro del ejecutable toda la lógica necesaria de la aplicación, dejando como requisitos externos únicamente Windows, Edge, red/VPN y Outlook clásico cuando se utilice correo.
- Detecte correctamente Edge instalado.
- Realice login visible y automatizado contra el entorno autorizado.
- Genere el TOTP usando la hora del portal.
- Ejecute correctamente el modo de prueba sin crear solicitudes.
- Ejecute correctamente el modo real bajo confirmación explícita.
- Gestione conflictos y paginación.
- Valide y envíe correo desde Outlook clásico cuando corresponda.
- No cree archivos operativos persistentes ni exponga secretos en mensajes, capturas o diagnósticos.
- Pase las pruebas unitarias y de integración definidas.
- Tenga un ejecutable portable versionado y documentación de uso.

## Fuentes técnicas autorizadas

Para decisiones de implementación, consultar prioritariamente documentación oficial:

- [WPF — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [WinUI 3 — Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [Publicación de aplicaciones .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Single-file deployment .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Playwright .NET](https://playwright.dev/dotnet/docs/library)
- [Navegadores y canales de Playwright .NET](https://playwright.dev/dotnet/docs/browsers)
- [Outlook MailItem.SendUsingAccount](https://learn.microsoft.com/en-us/office/vba/api/outlook.mailitem.sendusingaccount)
- [Outlook MailItem.Send](https://learn.microsoft.com/en-us/office/vba/api/outlook.mailitem.send%28method%29)
- [Introducción a MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-intro)
- [Configuración de MSTest SDK](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-sdk)
- [Guardar y cargar contenido de RichTextBox en WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-save-load-and-print-richtextbox-content)

No sustituir documentación oficial por suposiciones del modelo.

## Estrategia de migración desde la aplicación actual

La versión actual ubicada en:

```text
C:\pam automatizacion
```

está escrita en JavaScript/Node.js y debe tratarse como una referencia funcional e histórica. La nueva aplicación se construirá desde cero en:

```text
C:\pam automatizacion dotnet\PamAutomation
```

La aplicación anterior no debe modificarse durante la construcción de la nueva versión.

La aplicación anterior debe utilizarse para conocer:

- El comportamiento funcional comprobado.
- Las reglas del portal PAM.
- Los errores ya descubiertos.
- La apariencia y experiencia de usuario.
- Los selectores y textos observados en el portal.
- Las reglas de TOTP, solicitudes, conflictos y correo.

La aplicación anterior no debe utilizarse para copiar:

- La arquitectura JavaScript.
- Node.js.
- `playwright-core`.
- El servidor HTTP local.
- PowerShell.
- Archivos `.cmd`.
- `.env` o cualquier otra forma de almacenar secretos en texto plano.
- La estructura de carpetas de la aplicación anterior.
- Decisiones técnicas que solo existían por las limitaciones de la implementación anterior.

La interfaz anterior debe ser una referencia visual. La nueva interfaz debe implementarse con WPF/XAML y C#, no copiando HTML, CSS o JavaScript.

## Orden de autoridad de la información

Cuando existan diferencias entre fuentes, utilizar este orden:

1. Decisiones confirmadas explícitamente por el usuario.
2. Este `CODEX_BUILD_SPEC.md`.
3. `README.md`, `LOGIC.md` y `History.md` del proyecto anterior.
4. Comportamiento comprobado de la aplicación anterior.
5. Código fuente anterior.
6. Suposiciones de Codex.

Si una fuente contradice a otra, Codex debe detenerse, describir la contradicción y solicitar una decisión. No debe elegir silenciosamente una de las versiones.

## Lectura inicial del agente principal

En el nuevo proyecto, ubicado en `C:\pam automatizacion dotnet\PamAutomation`, deben existir como mínimo:

```text
C:\pam automatizacion dotnet\PamAutomation\AGENTS.md
C:\pam automatizacion dotnet\PamAutomation\CODEX_BUILD_SPEC.md
```

La primera vez que un thread nuevo entra al proyecto sin contexto previo, el agente principal debe leer completamente esos dos archivos y las demás lecturas vigentes definidas en `AGENTS.md`. Esta lectura inicial se realiza una sola vez durante la vida de ese thread. Las nuevas tareas, fases o solicitudes dentro del mismo thread deben usar el contexto conservado y el índice de `AGENTS.md` para consultar solo lo necesario. La lectura completa solo se repite si otro thread o coordinador entra sin contexto suficiente, aparece una contradicción que exige reconciliar toda la documentación o el usuario la solicita expresamente.

Después, cuando la fase lo necesite, el agente principal debe inspeccionar como referencia de solo lectura las partes pertinentes del proyecto anterior:

```text
C:\pam automatizacion\README.md
C:\pam automatizacion\LOGIC.md
C:\pam automatizacion\History.md
C:\pam automatizacion\src
C:\pam automatizacion\web
C:\pam automatizacion\test
```

No se deben copiar al nuevo proyecto `node_modules`, `.env`, `.env.mail`, `logs`, `run-artifacts`, `run-results`, cookies, perfiles de navegador ni secretos.

Los subagentes no repiten estas lecturas completas. El coordinador debe proporcionarles el contexto, la ruta absoluta del worktree, la rama, los archivos permitidos y las secciones exactas que necesiten. Un subagente solo puede ampliar la lectura para resolver una dependencia directa, un riesgo de seguridad o una contradicción, y debe informar esa ampliación. La lectura global queda reservada para tareas que el coordinador defina expresamente como auditorías, revisiones arquitectónicas o reconciliaciones documentales.


## Reglas de colaboración con Codex

- Codex debe inspeccionar el estado actual antes de modificar archivos.
- Cada tarea debe tener un alcance pequeño y una lista explícita de archivos permitidos.
- El agente principal debe conservar el contexto global; los subagentes recibirán contexto mínimo suficiente y lecturas documentales concretas conforme a `Subagentes.md`.
- No pedir a todos los subagentes que relean toda la documentación o el repositorio cuando su tarea sea acotada.
- No utilizar más de dos subagentes en un mismo task. El límite no obliga a usar ambos cuando no exista paralelismo real.
- Si un cambio es pequeño, está concentrado en pocos archivos o no puede avanzar en paralelo con otra unidad independiente, el agente principal debe implementarlo directamente para evitar consumo adicional de tokens y coordinación.
- Cada subagente de implementación debe recibir una unidad vertical independiente y hacerse cargo del código, las pruebas relacionadas y la validación focalizada de su alcance.
- No separar entre dos subagentes la implementación y las pruebas de una misma característica cuando uno deba esperar al otro.
- Fijar primero los contratos y archivos compartidos; ejecutar el trabajo independiente por oleadas e integrar un resultado a la vez.
- Codex debe restaurar cuando corresponda, compilar y ejecutar desde la terminal las pruebas relacionadas con cada cambio.
- No aceptar frases como “debería funcionar” sin evidencia de compilación o pruebas.
- Si una prueba falla, reparar únicamente la causa demostrada.
- No permitir que Codex avance automáticamente a otra fase después de una tarea incompleta.
- Revisar manualmente cada cambio que pueda crear solicitudes PAM, enviar correo o manipular credenciales.
- Usar pruebas falsas para Outlook y simulaciones del portal antes de hacer pruebas reales.
- No ejecutar pruebas reales contra PAM sin autorización explícita.
- No enviar correos automáticamente durante pruebas.
- No modificar el código anterior.

Codex puede leer el código completo de la aplicación anterior, pero la nueva implementación debe conservar el comportamiento y reconstruir la solución con la arquitectura C# definida aquí.

## Criterio de migración correcta

La migración no se considera correcta porque la nueva aplicación tenga una apariencia similar o compile. Debe demostrar que conserva los comportamientos críticos:

- Autenticación con TOTP sincronizado con la hora HTTP de PAM.
- Reintento único ante rechazo explícito del TOTP.
- Restricción de origen y certificado limitada al portal permitido.
- Modo de prueba que nunca presiona `Submit Request`.
- Modo real protegido por confirmación.
- Validación doble de todos los campos antes de crear solicitudes.
- Paginación completa de `Pending`.
- Detección y registro de conflictos.
- Protección contra envíos de correo duplicados.
- Integración con la cuenta correcta de Outlook clásico.
- Ausencia de logs, resultados, capturas y artefactos persistentes en el uso normal.
- Publicación autocontenida que utiliza Edge externo.
