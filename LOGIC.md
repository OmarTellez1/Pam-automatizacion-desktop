# Lógica prevista — Pam Automation .NET

## Estado

Este documento describe la lógica objetivo de la aplicación nueva. La implementación todavía no existe.

## Capas

- WPF/XAML: interfaz y presentación.
- Core: modelos, reglas, validación, TOTP e interfaces.
- Browser: Playwright .NET, Edge externo y portal PAM.
- Outlook: integración con Outlook clásico mediante COM.
- Infrastructure: DPAPI o Credential Manager, archivos de configuración, reloj y dependencias del sistema.
- Tests: unitarias, simulaciones del portal y pruebas controladas de integración.

La lógica de negocio no debe vivir en el code-behind de las ventanas WPF.

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

La configuración de correo se guardará en:

    %LOCALAPPDATA%\PamAutomation\config\mail-settings.dat

El archivo contendrá remitente, Para, CC, asunto y HTML enriquecido con el marcador REQUEST_IDS. Debe protegerse con DPAPI CurrentUser.

La única persistencia propia de la aplicación será:

    %LOCALAPPDATA%\PamAutomation\config\
    ├── app-settings.json
    └── mail-settings.dat

No persistir `results`, `logs`, `artifacts`, `temp`, `last-run`, `last-email` ni `delivery-state`. El resultado de una ejecución se representará mediante un modelo en memoria que contenga Request ID, conflictos, estado y errores. La interfaz lo mostrará mientras la aplicación permanezca abierta.

Si la acción final es correo, la aplicación debe preparar, validar y enviar el mensaje dentro de la misma ejecución. La protección contra duplicados debe funcionar durante esa ejecución; no se conservará un historial local después de cerrar la aplicación.

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

Los locators se refieren al DOM HTML del portal externo. La interfaz propia no utilizará HTML, CSS ni JavaScript. Priorizar roles, labels y atributos estables; CSS o XPath solo como último recurso.

## Solicitudes

La aplicación debe separar claramente:

- Modo prueba: llena y valida formularios, descarta cada formulario y nunca pulsa Submit Request.
- Modo real: requiere confirmación y crea solicitudes.

El modo real debe recorrer todas las cuentas de Favorites, todas las páginas y las solicitudes Pending relevantes. Debe validar sistema, aplicación, cuenta, familia, fecha, hora, duración y razón antes de cada envío.

El conflicto PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable se registra y permite continuar. Otros errores deben detener el proceso.

## Outlook

Outlook clásico se integrará mediante COM. Debe verificarse la cuenta exacta por SmtpAddress, asignarse SendUsingAccount antes de Send() y crearse un mensaje nuevo por ejecución.

La integración COM debe ejecutarse en STA, liberar referencias y no enviar mensajes durante pruebas automáticas.

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

Las pruebas unitarias no deben abrir Edge, PAM ni Outlook. Las pruebas de integración visibles pueden utilizar credenciales reales y el contexto temporal de Playwright cuando exista autorización explícita; deben conservar los mismos controles de seguridad y no deben crear solicitudes ni enviar correos sin una autorización adicional.
