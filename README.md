Vosk работает через WebSocket, что часто может быть неудобно.

Данная REST API принимает *.wav 8000 KHz 16 bit и возвращает либо JSON, либо text/plain в зависимости от хедера Accept.

Конвертация *.mp3 файлов была добавлена, но в релизе не работает из-за AOT :(

Для быстрого использования надо развернуть контейнеры из docker-compose.vosk.yml. Предварительно стоит поменять
- VoskSettings:WebSocketUrl в appsettings.json 
- linux-arm64 на нужную архитектуру процессора в Dockerfile
