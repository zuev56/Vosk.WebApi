# REST API для работы с VOSK (ASR).

Vosk работает через WebSocket, что часто может быть неудобно.

Данная REST API принимает
- *.wav
- *.mp3
- *.wma
- *.ogg

Если работать только с *.wav 8000 KHz 16 bit конвертер не потребуется.

Возвращает либо JSON (детальный вывод), либо text/plain в зависимости от хедера Accept.

Пример вызова:
```
curl http://my-server:7075/vosk/transcribe \
  --request POST \
  --header 'Content-Type: multipart/form-data' \
  --header 'Accept: text/plain' \
  --form 'input.mp3=@input.mp3'
```

**Перед развёртыванием при необходимости стоит заменить linux-amd64 на нужную архитектуру процессора в \Vosk.WebApi\Dockerfile**
