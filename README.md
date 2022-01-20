## Sample ADT Query 

dtId 충진실의 모든 센서
```
SELECT Sensor FROM DIGITALTWINS Room JOIN Sensor RELATED Room.hasCapability WHERE Room.$dtId = '충진실' AND IS_PRIMITIVE(Sensor.Value)
```

dtId 충진실의 모든 particle 센서
```
SELECT Sensor FROM DIGITALTWINS Room JOIN Sensor RELATED Room.hasCapability WHERE IS_OF_MODEL(Sensor, 'dtmi:com:iloh:demo:sensor:particle;1') AND Room.$dtId = '충진실' AND IS_PRIMITIVE(Sensor.Value)
```

