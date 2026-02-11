// ============================================================================
// CONFIG.INO - Configuration et certificats
// ============================================================================

#include <WiFiS3.h>
#include <WiFiSSLClient.h>
#include <ArduinoMqttClient.h>
#include <ArduinoJson.h>
#include <WebSocketsServer.h>

// Configuration réseau
const char* ssid = "Bard";
const char* password = "PipiEntreAmis67";
const char* mqtt_server = "10.0.0.67";
const int mqtt_port = 8883;
const char* admin_password = "admin123";

// Intervalles
const unsigned long BROADCAST_INTERVAL = 5000;  // Broadcast toutes les 5s

// Certificat CA
const char ca_cert[] = R"EOF(
-----BEGIN CERTIFICATE-----
MIIDtTCCAp2gAwIBAgIUETruQ+JBEU7Nj8r5CCPkbDlQ0/YwDQYJKoZIhvcNAQEL
BQAwajELMAkGA1UEBhMCRlIxETAPBgNVBAgMCEdyYW5kRXN0MRMwEQYDVQQHDApT
dHJhc2JvdXJnMRAwDgYDVQQKDAdGaWF0THV4MQwwCgYDVQQLDANJb1QxEzARBgNV
BAMMCkZpYXRMdXgtQ0EwHhcNMjYwMjA2MDkzODMxWhcNMzYwMjA0MDkzODMxWjBq
MQswCQYDVQQGEwJGUjERMA8GA1UECAwIR3JhbmRFc3QxEzARBgNVBAcMClN0cmFz
Ym91cmcxEDAOBgNVBAoMB0ZpYXRMdXgxDDAKBgNVBAsMA0lvVDETMBEGA1UEAwwK
RmlhdEx1eC1DQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAJD0wcgE
ykRV7bawZD42L/YiWMCW7j/6w9H4gy4P2P1MBfQWCXAYo1cvrgYfIkCL2B05OqdI
J6LYHRXiRa9RHDpXA2XfAsJMaA3wn77Zi/8kHyF/70rrLuy+T5bfvp8aAAv9Fiv/
x8bqIlhHQ/cRYJbZ49o8aYTdaeUyBcoTW34wxoGwY+SjEpTUUt5QVbt2nn1V8jT1
3Vuy4tu9JewKwBhZVBBTcDCDKQpa+ZRS21F8UOQvzlxXWG+X4guKQXoUYXK+C8JJ
4PD21yHmWb8cJCPZMhQAxokiK5/G82cQFPLSnOP3/fjjLUl/FdV+YD3oIBMbno0c
J1FLqxAfpps9VBUCAwEAAaNTMFEwHQYDVR0OBBYEFGOdWA/Izka2Tc+zkhpOjWca
EEjcMB8GA1UdIwQYMBaAFGOdWA/Izka2Tc+zkhpOjWcaEEjcMA8GA1UdEwEB/wQF
MAMBAf8wDQYJKoZIhvcNAQELBQADggEBAClcV0zm05YBmcQ6eWtrpvBSu/Zhr5Hl
ziyU/n25uAkdxJ4suudQ10iaYKGYhUWA4Cu51fi046oIxEYH4od5j9pDBhx7L17i
8164me7Xy67BaVbGnZlM9F8LlknB9mwrCjp8shJzh7RfDUOxNUiMwbx+++lnHECn
1JV3DXHKIIBokSP93bJNEdU7G6K0zqc6g/qfckAERMIBkPU7SYyYnX04Xg1bo9qV
bVs6Tz3Vq7Pv26FaItZMx0Q6+t+ZSUp0aX9NRh/ybNwOTvacSFUYAqbc2lG0bw7U
lsYNdZGljAo4//n5ag2GKU18b4XERJP5/izSjGgc6lIPTrUdra7ipYE=
-----END CERTIFICATE-----
)EOF";


// ============================================================================
// STRUCTURES.INO - Toutes les structures de données
// ============================================================================

// Session admin
struct AdminSession {
  String sessionId;
  String token;
  unsigned long createdAt;
};

// Capteur
struct Sensor {
  String sensorId;           // ID unique (ex: "PHOTO_201")
  String roomId;             // Salle associée
  String type;               // Type de capteur (ex: "photoresistor")
  float value;               // Dernière valeur mesurée
  String mode;               // "jour" ou "nuit"
  unsigned long uptime;      // Uptime du capteur
  unsigned long lastUpdate;  // Dernière donnée reçue
  unsigned long lastHeartbeat; // Dernier heartbeat
  String status;             // "online", "sleeping", "offline"
  bool active;               // Capteur actif ou non
};

// Lampe
struct Lamp {
  String lampId;             // ID unique (ex: "LED_1")
  String roomId;             // Salle associée
  int power;                 // Puissance 0-100%
  int pwm;                   // PWM 0-255
  String mode;               // Mode actuel
  int target;                // Target lux
  unsigned long uptime;      // Uptime de la lampe
  unsigned long lastUpdate;  // Dernière donnée reçue
  unsigned long lastHeartbeat; // Dernier heartbeat
  String status;             // "on", "off", "sleeping", "offline"
  bool active;               // Lampe active ou non
};

// Historique de mesures
struct RoomReading {
  float lux;
  unsigned long timestamp;
};

// Salle
struct RoomData {
  String roomId;
  bool active = false;
  bool isManualMode = false;
  
  // Historique FIFO (20 mesures)
  RoomReading history[20];
  int historyCount = 0;
  int historyIndex = 0;
};

// ============================================================================
// VARIABLES GLOBALES
// ============================================================================

// Système d'appareils
Sensor sensors[10];         // Max 10 capteurs
int sensorCount = 0;
Lamp lamps[20];             // Max 20 lampes
int lampCount = 0;

// Salles
RoomData rooms[2];
int roomCount = 0;

// Sessions admin
AdminSession adminSessions[10];
int sessionCount = 0;

// WebSocket & MQTT
WiFiSSLClient wifiClient;
MqttClient mqttClient(wifiClient);
WebSocketsServer wsServer(81);
bool clientWasConnected[4] = {false, false, false, false};

unsigned long lastBroadcast = 0;



// ============================================================================
// ROOMS.INO - Gestion des salles
// ============================================================================

int getRoomIndex(String roomId) {
  // Cherche si la salle existe
  for (int i = 0; i < 2; i++) {
    if (rooms[i].active && rooms[i].roomId == roomId) {
      return i;
    }
  }
  
  // Cree nouvelle salle
  for (int i = 0; i < 2; i++) {
    if (!rooms[i].active) {
      rooms[i].active = true;
      rooms[i].roomId = roomId;
      roomCount++;
      Serial.print(">>> Nouvelle salle: ");
      Serial.println(roomId);
      return i;
    }
  }
  
  Serial.println("!!! Limite 2 salles atteinte");
  return -1;
}

void addToRoomHistory(String roomId, float lux, unsigned long timestamp) {
  int roomIndex = getRoomIndex(roomId);
  if (roomIndex < 0) return;
  
  RoomData &room = rooms[roomIndex];
  room.history[room.historyIndex].lux = lux;
  room.history[room.historyIndex].timestamp = timestamp;
  room.historyIndex = (room.historyIndex + 1) % 20;
  if (room.historyCount < 20) room.historyCount++;
}



// ============================================================================
// SENSORS.INO - Gestion des capteurs
// ============================================================================

int findSensor(String sensorId) {
  for (int i = 0; i < sensorCount; i++) {
    if (sensors[i].sensorId == sensorId && sensors[i].active) {
      return i;
    }
  }
  return -1;
}

int registerSensor(String sensorId, String roomId) {
  // Cherche si deja enregistre
  int idx = findSensor(sensorId);
  if (idx >= 0) return idx;
  
  // Enregistre nouveau capteur
  if (sensorCount < 10) {
    sensors[sensorCount].sensorId = sensorId;
    sensors[sensorCount].roomId = roomId;
    sensors[sensorCount].type = "photoresistor";
    sensors[sensorCount].active = true;
    sensors[sensorCount].lastUpdate = 0;
    sensors[sensorCount].lastHeartbeat = 0;
    sensors[sensorCount].status = "offline";
    
    Serial.print(">>> Capteur enregistre: ");
    Serial.print(sensorId);
    Serial.print(" -> Salle ");
    Serial.println(roomId);
    
    return sensorCount++;
  }
  
  Serial.println("!!! Limite 10 capteurs atteinte");
  return -1;
}

void updateSensor(JsonDocument &doc, unsigned long now) {
  String sensorId = doc["sensorId"];
  String roomId = doc["room"];
  
  // Enregistre le capteur si nouveau
  int idx = registerSensor(sensorId, roomId);
  if (idx < 0) return;
  
  Sensor &sensor = sensors[idx];
  
  // Mise a jour des donnees
  sensor.value = doc["lux"];
  sensor.mode = doc.containsKey("mode") ? doc["mode"].as<String>() : "jour";
  sensor.uptime = doc.containsKey("uptime") ? doc["uptime"] : 0;
  
  bool isHeartbeat = doc.containsKey("heartbeat") ? doc["heartbeat"] : false;
  
  if (isHeartbeat) {
    sensor.lastHeartbeat = now;
  } else {
    sensor.lastUpdate = now;
  }
  
  // Calcul du statut
  updateSensorStatus(idx);
  
  // Ajoute a l'historique
  addToRoomHistory(roomId, sensor.value, now);
}

void updateSensorStatus(int idx) {
  if (idx < 0 || idx >= sensorCount || !sensors[idx].active) return;
  
  Sensor &sensor = sensors[idx];
  unsigned long timeSinceUpdate = millis() - sensor.lastUpdate;
  unsigned long timeSinceHeartbeat = millis() - sensor.lastHeartbeat;
  
  if (sensor.mode == "nuit") {
    // Mode nuit: on attend juste le heartbeat (2 min)
    if (timeSinceHeartbeat < 150000) {
      sensor.status = "sleeping";
    } else {
      sensor.status = "offline";
    }
  } else {
    // Mode jour: on verifie les updates regulieres (10s)
    if (timeSinceUpdate < 20000) {
      sensor.status = "online";
    } else if (timeSinceHeartbeat < 150000) {
      sensor.status = "sleeping";
    } else {
      sensor.status = "offline";
    }
  }
}

void updateAllSensorStatuses() {
  for (int i = 0; i < sensorCount; i++) {
    if (sensors[i].active) {
      updateSensorStatus(i);
    }
  }
}



// ============================================================================
// LAMPS.INO - Gestion des lampes
// ============================================================================

int findLamp(String lampId) {
  for (int i = 0; i < lampCount; i++) {
    if (lamps[i].lampId == lampId && lamps[i].active) {
      return i;
    }
  }
  return -1;
}

int registerLamp(String lampId, String roomId) {
  // Cherche si deja enregistree
  int idx = findLamp(lampId);
  if (idx >= 0) return idx;
  
  // Enregistre nouvelle lampe
  if (lampCount < 20) {
    lamps[lampCount].lampId = lampId;
    lamps[lampCount].roomId = roomId;
    lamps[lampCount].active = true;
    lamps[lampCount].lastUpdate = 0;
    lamps[lampCount].lastHeartbeat = 0;
    lamps[lampCount].status = "offline";
    
    Serial.print(">>> Lampe enregistree: ");
    Serial.print(lampId);
    Serial.print(" -> Salle ");
    Serial.println(roomId);
    
    return lampCount++;
  }
  
  Serial.println("!!! Limite 20 lampes atteinte");
  return -1;
}

void updateLamp(JsonDocument &doc, unsigned long now) {
  String lampId = doc["lampId"];
  String roomId = doc["room"];
  
  // Enregistre la lampe si nouvelle
  int idx = registerLamp(lampId, roomId);
  if (idx < 0) return;
  
  Lamp &lamp = lamps[idx];
  
  // Mise a jour des donnees
  lamp.power = doc.containsKey("power") ? doc["power"] : lamp.power;
  lamp.pwm = doc.containsKey("pwm") ? doc["pwm"] : lamp.pwm;
  lamp.mode = doc.containsKey("mode") ? doc["mode"].as<String>() : lamp.mode;
  lamp.target = doc.containsKey("target") ? doc["target"] : lamp.target;
  lamp.uptime = doc.containsKey("uptime") ? doc["uptime"] : 0;
  
  bool isHeartbeat = doc.containsKey("heartbeat") ? doc["heartbeat"] : false;
  
  if (isHeartbeat) {
    lamp.lastHeartbeat = now;
  } else {
    lamp.lastUpdate = now;
  }
  
  // Calcul du statut
  updateLampStatus(idx);
}

void updateLampStatus(int idx) {
  if (idx < 0 || idx >= lampCount || !lamps[idx].active) return;
  
  Lamp &lamp = lamps[idx];
  unsigned long timeSinceUpdate = millis() - lamp.lastUpdate;
  unsigned long timeSinceHeartbeat = millis() - lamp.lastHeartbeat;
  
  if (timeSinceUpdate < 20000) {
    lamp.status = lamp.power > 0 ? "on" : "off";
  } else if (timeSinceHeartbeat < 150000) {
    lamp.status = "sleeping";
  } else {
    lamp.status = "offline";
  }
}

void updateAllLampStatuses() {
  for (int i = 0; i < lampCount; i++) {
    if (lamps[i].active) {
      updateLampStatus(i);
    }
  }
}

void updateAllDeviceStatuses() {
  updateAllSensorStatuses();
  updateAllLampStatuses();
}



// ============================================================================
// ADMINSESSIONS.INO - Gestion des sessions admin
// ============================================================================

bool isValidAdminSession(String sessionId, String token) {
  for (int i = 0; i < sessionCount; i++) {
    if (adminSessions[i].sessionId == sessionId && 
        adminSessions[i].token == token &&
        sessionId != "" && token != "") {
      return true;
    }
  }
  return false;
}

void addAdminSession(String sessionId, String token) {
  // Cherche si session existe (mise a jour)
  for (int i = 0; i < sessionCount; i++) {
    if (adminSessions[i].sessionId == sessionId) {
      adminSessions[i].token = token;
      adminSessions[i].createdAt = millis();
      Serial.print("OK Session mise a jour: ");
      Serial.println(sessionId);
      return;
    }
  }
  
  // Nouvelle session
  if (sessionCount < 10) {
    adminSessions[sessionCount].sessionId = sessionId;
    adminSessions[sessionCount].token = token;
    adminSessions[sessionCount].createdAt = millis();
    sessionCount++;
    Serial.print("OK Nouvelle session: ");
    Serial.println(sessionId);
  } else {
    Serial.println("!!! Limite 10 sessions atteinte");
  }
}



// ============================================================================
// MQTT.INO - Gestion MQTT
// ============================================================================

void mqttMessageReceived(int messageSize) {
  String topic = mqttClient.messageTopic();
  String msg = mqttClient.readString();
  
  StaticJsonDocument<512> doc;
  if (deserializeJson(doc, msg)) {
    Serial.println("ERREUR: JSON invalide");
    return;
  }
  
  unsigned long now = millis();
  
  // Messages capteurs
  if (topic.startsWith("campus/capteur/")) {
    updateSensor(doc, now);
  }
  // Messages lampes
  else if (topic.startsWith("campus/lampe/")) {
    updateLamp(doc, now);
  }
  
  broadcastData();
}

void publishConfig(String roomId, String mode, int target) {
  String topic = "campus/config/" + roomId;
  String payload = "{\"mode\":\"" + mode + "\",\"target\":" + String(target) + "}";
  
  mqttClient.beginMessage(topic);
  mqttClient.print(payload);
  mqttClient.endMessage();
  
  Serial.print(">>> Config -> ");
  Serial.println(payload);
}

void reconnectMQTT() {
  if (mqttClient.connected()) return;
  
  Serial.print("MQTT...");
  
  if (mqttClient.connect(mqtt_server, mqtt_port)) {
    Serial.println(" OK");
    mqttClient.subscribe("campus/capteur/#");
    mqttClient.subscribe("campus/lampe/#");
    mqttClient.subscribe("campus/config/#");
  } else {
    Serial.print(" ERREUR Code: ");
    Serial.println(mqttClient.connectError());
  }
}



// ============================================================================
// WEBSOCKET.INO - Gestion WebSocket
// ============================================================================

void broadcastData() {
  if (wsServer.connectedClients() == 0) return;
  
  StaticJsonDocument<4096> doc;
  JsonArray roomsArray = doc.createNestedArray("rooms");
  
  // Pour chaque salle
  for (int i = 0; i < 2; i++) {
    if (!rooms[i].active) continue;
    
    JsonObject room = roomsArray.createNestedObject();
    room["roomId"] = rooms[i].roomId;
    room["isManualMode"] = rooms[i].isManualMode;
    
    // Capteurs de cette salle
    JsonArray sensorsArray = room.createNestedArray("sensors");
    for (int j = 0; j < sensorCount; j++) {
      if (sensors[j].active && sensors[j].roomId == rooms[i].roomId) {
        JsonObject s = sensorsArray.createNestedObject();
        s["sensorId"] = sensors[j].sensorId;
        s["type"] = sensors[j].type;
        s["value"] = sensors[j].value;
        s["mode"] = sensors[j].mode;
        s["uptime"] = sensors[j].uptime;
        s["lastUpdate"] = sensors[j].lastUpdate;
        s["lastHeartbeat"] = sensors[j].lastHeartbeat;
        s["status"] = sensors[j].status;
        
        // Pour compatibilite
        room["lux"] = sensors[j].value;
      }
    }
    
    // Lampes de cette salle
    JsonArray lampsArray = room.createNestedArray("lamps");
    for (int j = 0; j < lampCount; j++) {
      if (lamps[j].active && lamps[j].roomId == rooms[i].roomId) {
        JsonObject l = lampsArray.createNestedObject();
        l["lampId"] = lamps[j].lampId;
        l["power"] = lamps[j].power;
        l["pwm"] = lamps[j].pwm;
        l["mode"] = lamps[j].mode;
        l["target"] = lamps[j].target;
        l["uptime"] = lamps[j].uptime;
        l["lastUpdate"] = lamps[j].lastUpdate;
        l["lastHeartbeat"] = lamps[j].lastHeartbeat;
        l["status"] = lamps[j].status;
        
        // Pour compatibilite
        room["lampPower"] = lamps[j].power;
        room["targetLux"] = lamps[j].target;
        room["mode"] = lamps[j].mode;
      }
    }
    
    // Historique
    JsonArray historyArray = room.createNestedArray("history");
    int start = (rooms[i].historyIndex - rooms[i].historyCount + 20) % 20;
    for (int j = 0; j < rooms[i].historyCount; j++) {
      int idx = (start + j) % 20;
      JsonObject reading = historyArray.createNestedObject();
      reading["lux"] = rooms[i].history[idx].lux;
      reading["timestamp"] = rooms[i].history[idx].timestamp;
    }
  }
  
  doc["timestamp"] = millis();
  doc["sensorCount"] = sensorCount;
  doc["lampCount"] = lampCount;
  
  String output;
  serializeJson(doc, output);
  wsServer.broadcastTXT(output);
}

void sendRoomHistory(uint8_t num, String roomId) {
  int roomIndex = -1;
  for (int i = 0; i < 2; i++) {
    if (rooms[i].active && rooms[i].roomId == roomId) {
      roomIndex = i;
      break;
    }
  }
  
  if (roomIndex < 0) {
    wsServer.sendTXT(num, "{\"error\":\"Room not found\"}");
    return;
  }
  
  RoomData &room = rooms[roomIndex];
  
  StaticJsonDocument<2048> doc;
  doc["roomId"] = roomId;
  doc["count"] = room.historyCount;
  
  JsonArray historyArray = doc.createNestedArray("history");
  int start = (room.historyIndex - room.historyCount + 20) % 20;
  for (int i = 0; i < room.historyCount; i++) {
    int idx = (start + i) % 20;
    JsonObject reading = historyArray.createNestedObject();
    reading["lux"] = room.history[idx].lux;
    reading["timestamp"] = room.history[idx].timestamp;
  }
  
  String output;
  serializeJson(doc, output);
  wsServer.sendTXT(num, output);
}

void handleWebSocketCommand(uint8_t num, StaticJsonDocument<300> &doc) {
  String cmd = doc["command"].as<String>();
  
  // getRoomHistory
  if (cmd == "getRoomHistory" && doc.containsKey("roomId")) {
    sendRoomHistory(num, doc["roomId"].as<String>());
  }
  
  // requestAdmin
  else if (cmd == "requestAdmin" && doc.containsKey("password")) {
    if (doc["password"].as<String>() == admin_password) {
      String token = String(millis()) + String(random(10000));
      
      String sessionId;
      if (doc.containsKey("sessionId")) {
        sessionId = doc["sessionId"].as<String>();
        Serial.print("OK SessionId recu: ");
        Serial.println(sessionId);
      } else {
        sessionId = String(millis()) + "_" + String(num);
        Serial.print("WARN SessionId genere: ");
        Serial.println(sessionId);
      }
      
      addAdminSession(sessionId, token);
      
      String response = "{\"success\":true,\"adminToken\":\"" + token + "\",\"sessionId\":\"" + sessionId + "\"}";
      wsServer.sendTXT(num, response);
      Serial.print("OK Admin authentifie: ");
      Serial.println(sessionId);
    } else {
      wsServer.sendTXT(num, "{\"success\":false,\"error\":\"Invalid password\"}");
      Serial.println("ERREUR: Auth echouee");
    }
  }
  
  // setManualMode (admin)
  else if (cmd == "setManualMode" && doc.containsKey("roomId") && doc.containsKey("enabled")) {
    String token = doc.containsKey("adminToken") ? doc["adminToken"].as<String>() : "";
    String sessionId = doc.containsKey("sessionId") ? doc["sessionId"].as<String>() : "";
    
    if (isValidAdminSession(sessionId, token)) {
      String roomId = doc["roomId"].as<String>();
      bool enabled = doc["enabled"];
      
      int roomIndex = getRoomIndex(roomId);
      if (roomIndex >= 0) {
        rooms[roomIndex].isManualMode = enabled;
        
        Serial.print("OK Mode manuel ");
        Serial.print(enabled ? "active" : "desactive");
        Serial.print(" [");
        Serial.print(sessionId);
        Serial.println("]");
        
        if (!enabled) {
          publishConfig(roomId, "Libre", 500);
        }
        
        broadcastData();
      }
    } else {
      Serial.println("ERREUR: Session invalide");
    }
  }
  
  // setManualTarget (admin)
  else if (cmd == "setManualTarget" && doc.containsKey("roomId") && doc.containsKey("target")) {
    String token = doc.containsKey("adminToken") ? doc["adminToken"].as<String>() : "";
    String sessionId = doc.containsKey("sessionId") ? doc["sessionId"].as<String>() : "";
    
    if (isValidAdminSession(sessionId, token)) {
      publishConfig(doc["roomId"].as<String>(), "Manual", doc["target"]);
      Serial.print("OK Target: ");
      Serial.print(doc["target"].as<int>());
      Serial.print(" [");
      Serial.print(sessionId);
      Serial.println("]");
    } else {
      Serial.println("ERREUR: Session invalide");
    }
  }
  
  // setMode (users)
  else if (cmd == "setMode" && doc.containsKey("roomId") && doc.containsKey("mode") && doc.containsKey("target")) {
    String roomId = doc["roomId"].as<String>();
    int roomIndex = getRoomIndex(roomId);
    
    if (roomIndex >= 0 && !rooms[roomIndex].isManualMode) {
      publishConfig(roomId, doc["mode"].as<String>(), doc["target"]);
    } else if (roomIndex >= 0) {
      Serial.println("ERREUR: Salle verrouillee (mode manuel)");
    }
  }
}

void webSocketEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length) {
  switch(type) {
    case WStype_DISCONNECTED:
      if (clientWasConnected[num]) {
        clientWasConnected[num] = false;
        Serial.print("[");
        Serial.print(num);
        Serial.println("] Deco");
      }
      break;
      
    case WStype_CONNECTED:
      clientWasConnected[num] = true;
      Serial.print("[");
      Serial.print(num);
      Serial.println("] Co");
      
      if (roomCount > 0) {
        broadcastData();
        Serial.println(">>> Donnees initiales");
      }
      break;
      
    case WStype_TEXT:
      {
        StaticJsonDocument<300> doc;
        if (!deserializeJson(doc, (char*)payload) && doc.containsKey("command")) {
          handleWebSocketCommand(num, doc);
        }
      }
      break;
  }
}



// ============================================================================
// FIAT LUX BRIDGE v3 - FICHIER PRINCIPAL
// ============================================================================

void setup() {
  Serial.begin(115200);
  delay(1000);
  
  Serial.println("\n=== FIAT LUX BRIDGE v3 ===");
  Serial.println("Architecture modulaire");
  Serial.println("Max: 2 salles, 10 capteurs, 20 lampes");
  
  // WiFi
  WiFi.begin(ssid, password);
  Serial.print("WiFi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println(" OK");
  
  // Attends IP
  Serial.print("IP");
  int timeout = 0;
  while (WiFi.localIP() == IPAddress(0, 0, 0, 0) && timeout < 20) {
    delay(500);
    Serial.print(".");
    timeout++;
  }
  Serial.println();
  Serial.println(WiFi.localIP());
  
  if (WiFi.localIP() == IPAddress(0, 0, 0, 0)) {
    Serial.println("ERREUR: Pas d'IP !");
    while(1);
  }
  
  // WebSocket
  wsServer.begin();
  wsServer.onEvent(webSocketEvent);
  Serial.println("OK WebSocket:81");
  
  // MQTT TLS
  wifiClient.setCACert(ca_cert);
  mqttClient.setId("bridge_fiatlux_v3");
  mqttClient.onMessage(mqttMessageReceived);
  mqttClient.setKeepAliveInterval(60000);
  mqttClient.setConnectionTimeout(30000);
  
  reconnectMQTT();
  
  Serial.println("\n=== PRET ===");
  Serial.print("ws://");
  Serial.print(WiFi.localIP());
  Serial.println(":81");
}

void loop() {
  wsServer.loop();
  mqttClient.poll();
  
  // Reconnexion MQTT
  static unsigned long lastReconnect = 0;
  if (!mqttClient.connected() && millis() - lastReconnect > 5000) {
    lastReconnect = millis();
    reconnectMQTT();
  }
  
  // Broadcast periodique
  if (millis() - lastBroadcast >= BROADCAST_INTERVAL) {
    lastBroadcast = millis();
    if (roomCount > 0 && wsServer.connectedClients() > 0) {
      broadcastData();
    }
  }
  
  // Mise a jour des statuts
  static unsigned long lastStatusUpdate = 0;
  if (millis() - lastStatusUpdate >= 1000) {
    lastStatusUpdate = millis();
    updateAllDeviceStatuses();
  }
}