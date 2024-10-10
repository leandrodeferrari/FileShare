# Proyecto: Servidor Web con C Sharp

## Descripción

Este proyecto consiste en la creación de un servidor web simple utilizando sockets a nivel de capa de transporte, usando el protocolo TCP. El servidor es capaz de manejar solicitudes HTTP, responder a archivos HTML estáticos y procesar solicitudes GET y POST de manera concurrente. Además, permite la configuración externa de la carpeta donde se almacenan los archivos y del puerto, utilizando un archivo JSON.

## Requerimientos

- [x] Debe poder atender un número indefinido de solicitudes en forma concurrente.
- [x] Por defecto, deberá servir el archivo `index.html`, si la URL no especifica el archivo.
- [x] La carpeta desde donde se servirán los archivos debe ser configurable desde un archivo de configuración externo.
- [x] El puerto de escucha debe ser configurable desde un archivo de configuración externo.
- [x] En caso de que el usuario haya solicitado un archivo inexistente, deberá devolver un código de error 404 y un documento personalizado indicando el error.
- [x] Debe aceptar solicitudes de tipo GET y POST. En el caso de solicitudes POST, solamente deberan loguearse los datos recibidos.
- [x] Debe manejar parámetros de consulta desde la URL. En este caso, los parámetros solamente deberán loguearse.
- [x] Debe utilizar compresión de archivos para responder a las solicitudes.
- [x] Los datos de todas las solicitudes deben loguearse en un archivo por día, incluyendo la IP de origen.
- [x] Solamente deberan usar sockets (directamente en la capa de transporte) y se deben parsear las solicitudes HTTP. No se debe utilizar ninguna herramienta adicional.

## Tecnologías utilizadas

- C#
- .NET Framework 4.7.2
- Sockets para la comunicación a bajo nivel
- Newtonsoft.Json para manejo de JSON
- Compresión GZip para las respuestas HTTP
- Multithreading con ThreadPool para manejar solicitudes concurrentes.

## Pasos para correr el proyecto

### 1. Clonar el repositorio

Clona este repositorio en tu máquina local con el siguiente comando:

```bash
git clone https://github.com/leandrodeferrari/FileShare.git
```

### 2. Instalar el paquete Newtonsoft.Json

Para poder manejar archivos JSON en el proyecto, debes instalar el paquete Newtonsoft.Json. Utiliza el siguiente comando en la consola de tu IDE o el administrador de paquetes NuGet:

```bash
Install-Package Newtonsoft.Json
```

### 3. Crear la carpeta para las vistas

Debes crear la carpeta `C:\\view`  en tu sistema de archivos y copiar el contenido de la carpeta `/View` del repositorio dentro de esta nueva carpeta.

### 4. Configurar el archivo appsettings.json

Copia el siguiente contenido JSON dentro de un archivo llamado `appsettings.json` y colócalo en la carpeta `/bin/Debug` del proyecto.

```json
{
    "ServerConfig": {
      "FileServingPath": "C:\\view",
      "Port": 9999
    }
}
```

- FileServingPath: Es la ruta desde donde el servidor buscará los archivos para servir.
- Port: El puerto en el que el servidor escuchará las solicitudes.

### 5. Ejecutar el proyecto

Ejecuta el proyecto desde tu IDE (por ejemplo, Visual Studio) o desde la línea de comandos. Asegúrate de que los archivos de configuración y vistas están correctamente ubicados en las rutas especificadas.

### 6. Revisar los logs

Los logs de todas las solicitudes (incluyendo la IP del cliente, tipo de solicitud y parámetros) se registran automáticamente en archivos diarios dentro de la carpeta /bin/Debug/logs.
