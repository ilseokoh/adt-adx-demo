## Simulator 사용방법

### 사전설치

[Node.js 가 설치](https://nodejs.org/ko/download/)되어야 합니다. 


### 설정파일 (simulator-config.json)

simulator-config.json.temp 파일을 simulator-config.json 이름으로 변경합니다. 

### DPS 설정 

1. Azure Portal 을 이용해서 [DPS를 만들고 IoT Hub에 연결](https://docs.microsoft.com/ko-kr/azure/iot-dps/quick-setup-auto-provision)합니다. 
1. Azure Portal의 DPS 개요 메뉴에서 ID Scope / Global URL 을 찾아서 config 파일에 넣어줍니다. 
1. [대칭키 그룹 등록 만들기](https://docs.microsoft.com/ko-kr/azure/iot-dps/how-to-legacy-device-symm-key?tabs=windows#create-a-symmetric-key-enrollment-group)를 참조하여 Group Enrollment를 생성하고 생성된 대칭키를 config 파일에 입력합ㄴ디ㅏ. 

### 센서 시뮬레이터 설정 

index.js 는 config 설정에 따라서 각 센서 Simulator를 실행해 줍니다. 필요한 센선 Simulator를 설정해줍니다. 

1. 이름은 로그 메시지에 사용됩니다. 
2. count 는 실행할 센서 갯수를 의미합니다. 
3. intervalSec 로 설정된 주기마다 메시지를 보냅니다. 
4. prefix 는 센서 디바이스 아이디에 사용됩니다. 3개의 센서를 만든다면 temperature_01 / temperautre_02 / temperature_03 이렇게 3개를 만듭니다. 이 Device 아이디는 ADT의 dtid 와 같아야 합니다. dtid 는 ADT Explorer에서 instance 생성할때 넣는 이름입니다. 
5. filename 은 시뮬레이터 코드를 지정합니다. 

```json
{
    "name": "Temperature Sensor",
    "count": 1,
    "intervalSec": 60,
    "prefix": "temperature-",
    "filename": "temperature-sensor.js"
}
```


### 실행 
Simulators 폴더에서 아래 명령을 실행합니다. 
```
npm install 
node index --file .\simulator-config-sptek.json 
```