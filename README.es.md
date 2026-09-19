<p align="center">
  <strong>-------></strong>
  <a href="/README.md">Russian</a> |
  <a href="/README.en.md">English</a> |
  <a href="/README.es.md">Spanish</a> |
  <a href="/README.zh.md">Chinese</a> |
  <strong><-------</strong>
</p>

# Localización rusa de ZCode Desktop

Localización rusa (ru-RU) no oficial para ZCode Desktop, un cliente GUI para
un agente de programación con IA. La aplicación oficial solo incluye inglés y
chino; este proyecto añade una interfaz completa en ruso: ajustes, menús,
diálogos, bandeja del sistema, «Acerca de» (~4900 cadenas).

Funciona con **cualquier versión** de la aplicación: el instalador extrae
`app.asar`, localiza los archivos por firmas de código y aplica los parches.
Las cadenas sin traducir de versiones nuevas se muestran en inglés — nada se
rompe.

## Instalación (automática)

1. Cierre completamente ZCode (incluido el icono de la bandeja).
2. Descargue el archivo desde [Releases](../../releases) (archivo
   `zcode-ru-localization-vX.X.X.zip`), descomprímalo y ejecute
   **`РусификаторZCode.exe`**.
3. Elija la carpeta de ZCode → «Установить русский язык» (Instalar ruso).
4. Abra ZCode: la interfaz estará en ruso.

Alternativa sin GUI — PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File install-ru.ps1 [-InstallDir "C:\ruta\de\ZCode"]
```

Requisitos: [Node.js](https://nodejs.org) en el PATH.

## Desinstalar (revertir)

Botón «Откатить» en el programa, o manualmente: elimine `resources\app` y
renombre `resources\app.asar.orig` de nuevo a `resources\app.asar`.

## Después de actualizar ZCode

La actualización restaura el inglés. Simplemente ejecute el instalador de
nuevo; el parche se aplica a la nueva versión. Las cadenas aún sin traducir
se muestran en inglés.

## Cómo funciona

- Electron carga la carpeta `resources/app` **en lugar de** `app.asar` si
  existe. El instalador no reconstruye el archivo: coloca la carpeta parcheada
  junto a él (el original se guarda como `app.asar.orig`).
- `patch.mjs` localiza el catálogo de localización por la firma
  `g={"zh-CN":…,"en-US":…}` (los nombres de archivo cambian entre versiones),
  inyecta el catálogo ruso y añade `ru-RU` a los esquemas de validación,
  selectores de idioma y catálogos de la bandeja y «Acerca de».
- Antes de instalar, el parcheador comprueba la sintaxis de los archivos; ante
  cualquier error la instalación se cancela y la aplicación queda intacta.

## Contribuir traducciones

Todas las cadenas están en [`ru.json`](ru.json) (clave → texto). Si encuentra
algo sin traducir, añada el par y reinicie el instalador. No altere los
marcadores (`{version}`, `{count}`).

## Compilación del instalador

El exe se compila con el compilador de .NET Framework incluido en Windows;
ejecute `Build.bat` o:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -out:"РусификаторZCode.exe" InstallRu.cs -r:System.Windows.Forms.dll -r:System.Drawing.dll
```

El exe necesita junto a sí `patch.mjs`, `ru.json` y `node_modules`
(`npm install @electron/asar`).

## Aviso legal

Proyecto no oficial, sin relación con los desarrolladores de ZCode. Solo se
modifican los archivos de localización de su equipo. El autor no se hace
responsable de posibles problemas; siempre puede revertir los cambios.

## Licencia

[MIT](LICENSE)
