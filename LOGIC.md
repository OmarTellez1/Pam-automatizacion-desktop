# Lógica prevista — Pam Automation .NET

## Estado

Este documento describe la lógica objetivo de la aplicación nueva. Ya existen la estructura base y el estado de presentación de la ventana principal, pero la implementación funcional todavía no existe.

## Capas

- WPF/XAML: interfaz y presentación.
- Core: modelos, reglas, validación, TOTP e interfaces.
- Browser: Playwright .NET, Edge externo y portal PAM.
- Outlook: integración con Outlook clásico mediante COM.
- Infrastructure: DPAPI o Credential Manager, archivos de configuración, reloj y dependencias del sistema.
- Tests: unitarias, simulaciones del portal y pruebas controladas de integración.

La lógica de negocio no debe vivir en el code-behind de las ventanas WPF.

## Estado de presentación

La ventana principal utiliza MVVM mediante `CommunityToolkit.Mvvm`. El ViewModel expone el estado de cada configuración, los comandos visibles y las condiciones para habilitar acciones; el code-behind se limita a inicializar la ventana.

La aplicación comienza en estado incompleto y seguro. `Iniciar automatización` solo puede habilitarse cuando credenciales y solicitud estén configuradas y, si la acción final incluye correo, exista también una configuración de correo válida. Mientras una ejecución esté activa, los controles y comandos de configuración quedan deshabilitados.

El botón de prueba permanece separado del botón real. Solo es visible cuando `testModeEnabled` está activo y toda la configuración requerida es válida. En esta fase los botones de configuración y ejecución únicamente informan que la función aún no está implementada; no abren Edge u Outlook, no escriben configuración y no producen efectos externos.

## Persistencia y configuración

La aplicación no utilizará archivos .env.

Los secretos PAM se almacenarán en Windows Credential Manager, asociados al usuario actual de Windows. La aplicación recuperará estos valores únicamente cuando vaya a validar credenciales o iniciar una automatización:

- Usuario PAM.
- Contraseña PAM.
- Semilla TOTP.

La configuración general se guardará como JSON en:

    %LOCALAPPDATA%\PamAutomation\config\app-settings.json

Sus propiedades serán equivalentes a la configuración actual:

- finalAction equivale a PAM_FINAL_ACTION.
- requestReason equivale a PAM_REQUEST_REASON.
- sessionHours equivale a PAM_SESSION_HOURS.
- startDate equivale a PAM_START_DATE, persistida como fecha ISO.
- startTime equivale a PAM_START_TIME.
- timeZone debe ser America/Guayaquil.
- testModeEnabled habilita el modo de prueba y será `false` por defecto.

La configuración de correo se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat

El archivo contendrá remitente, Para, CC, asunto y HTML enriquecido con el marcador REQUEST_IDS. Debe protegerse con DPAPI CurrentUser.

La única persistencia propia de la aplicación será:

    %LOCALAPPDATA%\PamAutomation\config\
    ├── app-settings.json
    └── mail-settings.dat

No persistir `results`, `logs`, `artifacts`, `temp`, `last-run`, `last-email` ni `delivery-state`. El resultado de una ejecución se representará mediante un modelo en memoria que contenga Request ID, conflictos, estado y errores. La interfaz lo mostrará mientras la aplicación permanezca abierta.

Si la acción final es correo, la aplicación debe preparar, validar y enviar el mensaje dentro de la misma ejecución. La protección inmediata contra duplicados utilizará el estado en memoria de la ejecución. Antes de enviar, también se consultarán Enviados, Bandeja de salida y Borradores de Outlook para detectar un mensaje con el mismo asunto y el mismo conjunto exacto de Request ID. De este modo se conserva protección entre reinicios sin crear `last-email`, `delivery-state` ni otro historial local.

No habrá auditoría local ni debugging persistente por defecto. Un modo de diagnóstico futuro, si se necesita, deberá activarse explícitamente y exportar manualmente la información a una ubicación seleccionada por el desarrollador o soporte. No debe formar parte del almacenamiento normal.

Las escrituras de `app-settings.json` y `mail-settings.dat` deben ser atómicas, incluir `schemaVersion` y permitir migraciones futuras. No escribir configuración dentro de `Program Files` ni junto al ejecutable.

Windows o Playwright pueden utilizar `%TEMP%` para archivos internos transitorios durante el lanzamiento del navegador. No tratar esa ubicación como almacenamiento persistente, no escribir secretos allí y limpiar lo creado por la aplicación cuando sea posible.

## Diferencia entre desarrollo y aplicación publicada

La ausencia de archivos operativos persistentes aplica al ejecutable instalado que utilizan los compañeros. El repositorio de desarrollo sí necesita evidencias para investigar fallos y validar cambios.

Durante desarrollo, pruebas e integración controlada, se podrán crear en la raíz del repositorio:

    results\
    logs\
    artifacts\
    temp\

Estas carpetas estarán excluidas de Git y no se incluirán en el instalador. Contendrán únicamente resultados y diagnóstico técnico sin secretos. El perfil de publicación debe usar un almacenamiento en memoria y no debe escribir esas carpetas aunque el ejecutable se inicie desde otra ubicación.

## Autenticación

El flujo debe:

1. Abrir únicamente https://10.30.17.117.
2. Obtener la fecha del encabezado HTTP Date.
3. Calcular el TOTP con la hora del portal.
4. Esperar una ventana segura de vigencia.
5. Intentar el código una vez.
6. Si PAM lo rechaza explícitamente, esperar el siguiente intervalo y reintentar una sola vez.
7. No reintentar fallos de red o confirmación.
8. Confirmar el login con Return to dashboard.

La aplicación debe diferenciar credenciales rechazadas, TOTP rechazado, fallo de conexión y ausencia de confirmación.

## Navegador

Playwright .NET debe lanzar el Edge instalado mediante el canal msedge.

No se debe incluir Chromium. La aplicación debe utilizar la opción 1: lanzar el Edge instalado mediante el canal msedge y crear un contexto temporal de Playwright. No debe seleccionar, reutilizar ni modificar el perfil existente del usuario, sus cookies, extensiones o sesiones. No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente. El aislamiento es interno de Playwright y no debe presentarse como una ventana de incógnito.

La primera versión utilizará Edge visible para la comprobación de credenciales y la automatización real. Se conservará el comportamiento funcional validado de la aplicación anterior, no su uso de Chrome, perfiles existentes o ejecución headless. Cualquier modo headless futuro requerirá una decisión explícita y la misma cobertura de seguridad e integración.

Los locators se refieren al DOM HTML del portal externo. La interfaz propia no utilizará HTML, CSS ni JavaScript. Priorizar roles, labels y atributos estables; CSS o XPath solo como último recurso.

## Solicitudes

La aplicación debe separar claramente:

- Modo prueba: llena y valida formularios, descarta cada formulario y nunca pulsa Submit Request.
- Modo real: requiere confirmación y crea solicitudes.

El usuario podrá cambiar `testModeEnabled` mediante `Habilitar modo de prueba` en una sección avanzada de Configuración de solicitud; no necesitará editar el JSON. `Iniciar prueba` solo será visible cuando esa opción sea verdadera y la configuración de credenciales y solicitud esté completa. Si la acción final es correo, también deberá existir una configuración de correo válida, pero los destinatarios guardados no se usarán: el correo de prueba se enviará exclusivamente a `tellezoe@hitss.com`, sin CC, con asunto prefijado por `[PRUEBA]` y después de una confirmación que muestre el destinatario exacto.

El modo real debe recorrer todas las cuentas de Favorites, todas las páginas y las solicitudes Pending relevantes. Debe validar sistema, aplicación, cuenta, familia, fecha, hora, duración y razón antes de cada envío.

El conflicto PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable se registra y permite continuar. Otros errores deben detener el proceso.

## Outlook

Outlook clásico se integrará mediante COM. Debe verificarse la cuenta exacta por SmtpAddress, asignarse SendUsingAccount antes de Send() y crearse un mensaje nuevo por ejecución.

La integración COM debe ejecutarse en STA, liberar referencias y no enviar mensajes durante pruebas automáticas.

Las pruebas unitarias e integraciones automatizadas nunca enviarán correo. El envío de un correo de modo de prueba es una acción manual distinta, autorizada desde la interfaz y limitada a `tellezoe@hitss.com`.

## Editor enriquecido de correo

La interfaz utilizará `RichTextBox` y `FlowDocument` como modelo nativo de edición. Solo se admitirán capacidades declaradas y testeadas: párrafos, negrita, cursiva, subrayado, listas, tablas, imágenes pegadas y el marcador `{{REQUEST_IDS}}`. Un convertidor C# propio recorrerá el `FlowDocument` y generará un subconjunto permitido de HTML para Outlook; no se aceptará HTML arbitrario ni se afirmará compatibilidad completa.

El documento y sus imágenes se guardarán dentro de `mail-settings.dat` protegido con DPAPI CurrentUser. Las imágenes necesarias para adjuntos CID solo podrán materializarse como temporales durante la preparación del mensaje y deberán eliminarse al finalizar. Antes de desarrollar el editor completo se validará un prototipo de ida y vuelta, tablas, imágenes, marcador, saneamiento y renderizado en Outlook clásico.

## Seguridad

Los secretos deben almacenarse con Windows Credential Manager o DPAPI. No deben aparecer en capturas, excepciones mostradas al usuario, exportaciones de diagnóstico ni en ningún dato persistente.

## Resultados de ejecución

Cada ejecución mantendrá en memoria, sin persistirlo al cerrar la aplicación:

- Modo.
- Fecha y hora.
- Cuentas procesadas.
- Request ID.
- Conflictos.
- Errores.
- Acción final.
- Estado de confirmación del correo.

## Contratos de prueba

Todas las dependencias externas deben tener interfaces sustituibles:

- Reloj.
- Almacenamiento de secretos.
- Sistema de archivos.
- Automatización PAM.
- Outlook.

El framework elegido es MSTest sobre Microsoft Testing Platform. Las pruebas se descubrirán y ejecutarán desde Test Explorer de Visual Studio. Las pruebas COM que necesiten apartamento STA utilizarán el soporte STA de MSTest y permanecerán separadas de las pruebas unitarias.

Las pruebas unitarias no deben abrir Edge, PAM ni Outlook. Las pruebas de integración visibles pueden utilizar credenciales reales y el contexto temporal de Playwright cuando exista autorización explícita; deben conservar los mismos controles de seguridad y no deben crear solicitudes ni enviar correos sin una autorización adicional.
