#include <WiFiS3.h>
#include <WiFiSSLClient.h>
#include <ArduinoMqttClient.h>
#include <ArduinoJson.h>

// ============================================================================
// CONFIGURATION
// ============================================================================

const char* ssid = "Bard";
const char* password = "PipiEntreAmis67";
const char* mqtt_server = "10.0.0.67";
const int mqtt_port = 8883;
const char* roomId = "201";
const char* sensorId = "PHOTO_201";

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
// INTERVALLES
// ============================================================================

const unsigned long READ_INTERVAL = 1000;        // Lecture toutes les 1s
const unsigned long PUBLISH_INTERVAL = 10000;    // Publication toutes les 10s
const unsigned long HEARTBEAT_INTERVAL = 120000; // Heartbeat toutes les 2 min

// ============================================================================
// VARIABLES GLOBALES
// ============================================================================

WiFiSSLClient wifiClient;
MqttClient mqttClient(wifiClient);

const int photoPin = A0;

unsigned long lastRead = 0;
unsigned long lastPublish = 0;
unsigned long lastHeartbeat = 0;

float luxValues[10];
int valueIndex = 0;
int sampleCount = 0;

bool modeJour = true;
unsigned long bootTime = 0;

// ============================================================================
// FONCTION CALIBRATION
// ============================================================================

float rawToLux(int raw) {
  if (raw <= 1) return 0.0;
  if (raw <= 24) return (float)(raw - 1) / 23.0 * 800.0;
  if (raw <= 150) return 800.0 + (float)(raw - 24) / 126.0 * 12200.0;
  return 13000.0 + (float)(raw - 150) / 873.0 * 37000.0;
}

// ============================================================================
// PUBLICATION DES DONNÉES
// ============================================================================

void publishData(float lux, bool isHeartbeat) {
  StaticJsonDocument<256> doc;
  
  doc["sensorId"] = sensorId;
  doc["lux"] = round(lux * 10) / 10.0;
  doc["timestamp"] = millis();
  doc["room"] = roomId;
  doc["mode"] = modeJour ? "jour" : "nuit";
  doc["uptime"] = millis() - bootTime;
  doc["heartbeat"] = isHeartbeat;  // Indique si c'est un heartbeat
  
  String output;
  serializeJson(doc, output);
  
  String topic = String("campus/capteur/") + roomId;
  
  mqttClient.beginMessage(topic);
  mqttClient.print(output);
  
  if (mqttClient.endMessage()) {
    if (isHeartbeat) {
      Serial.print("HEARTBEAT: ");
    } else {
      Serial.print("DATA: ");
    }
    Serial.println(output);
  }
}

// ============================================================================
// GESTION MQTT
// ============================================================================

void onMqttMessage(int messageSize) {
  String topic = mqttClient.messageTopic();
  String msg = mqttClient.readString();
  
  if (topic == "campus/config/schedule") {
    StaticJsonDocument<128> doc;
    if (deserializeJson(doc, msg) == DeserializationError::Ok) {
      String mode = doc["mode"];
      
      if (mode == "jour") {
        modeJour = true;
        Serial.println("🌞 Mode JOUR activé");
      } else if (mode == "nuit") {
        modeJour = false;
        Serial.println("🌙 Mode NUIT activé (veille)");
      }
    }
  }
}

void reconnectMQTT() {
  if (mqttClient.connected()) return;
  
  Serial.print("MQTT...");
  
  String clientId = String("capteur_photo_") + roomId;
  mqttClient.setId(clientId.c_str());
  
  if (mqttClient.connect(mqtt_server, mqtt_port)) {
    Serial.println(" OK");
    mqttClient.subscribe("campus/config/schedule");
    Serial.println("✓ Abonné à campus/config/schedule");
  } else {
    Serial.print(" ERR:");
    Serial.println(mqttClient.connectError());
  }
}

// ============================================================================
// SETUP & LOOP
// ============================================================================

void setup() {
  Serial.begin(115200);
  delay(1000);
  
  bootTime = millis();
  
  Serial.println("\n=== CAPTEUR PHOTORÉSISTANCE v2 ===");
  Serial.print("Salle: ");
  Serial.println(roomId);
  Serial.print("Sensor ID: ");
  Serial.println(sensorId);
  
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println(" WiFi OK");
  Serial.println(WiFi.localIP());
  
  wifiClient.setCACert(ca_cert);
  mqttClient.onMessage(onMqttMessage);
  mqttClient.setKeepAliveInterval(60000);
  mqttClient.setConnectionTimeout(30000);
  
  reconnectMQTT();
  
  Serial.println("\n=== CONFIGURATION ===");
  Serial.println("Data: toutes les 10s");
  Serial.println("Heartbeat: toutes les 2 min");
  Serial.println("=== PRET ===\n");
}

void loop() {
  mqttClient.poll();
  
  // Reconnexion MQTT
  static unsigned long lastReconnect = 0;
  if (!mqttClient.connected() && millis() - lastReconnect > 5000) {
    lastReconnect = millis();
    reconnectMQTT();
  }
  
  unsigned long now = millis();
  
  // ============================================================================
  // HEARTBEAT (toutes les 2 minutes, même en mode nuit)
  // ============================================================================
  if (now - lastHeartbeat >= HEARTBEAT_INTERVAL) {
    lastHeartbeat = now;
    
    // Lecture instantanée pour le heartbeat
    int rawValue = analogRead(photoPin);
    float luxValue = rawToLux(rawValue);
    
    publishData(luxValue, true);  // heartbeat=true
  }
  
  // ============================================================================
  // MODE NUIT : arrête ici (sauf heartbeat)
  // ============================================================================
  if (!modeJour) {
    delay(1000);
    return;
  }
  
  // ============================================================================
  // MODE JOUR : mesures continues
  // ============================================================================
  
  // Lecture toutes les secondes
  if (now - lastRead >= READ_INTERVAL) {
    lastRead = now;
    
    int rawValue = analogRead(photoPin);
    float luxValue = rawToLux(rawValue);
    
    luxValues[valueIndex] = luxValue;
    valueIndex = (valueIndex + 1) % 10;
    if (sampleCount < 10) sampleCount++;
    
    Serial.print("Raw:");
    Serial.print(rawValue);
    Serial.print(" Lux:");
    Serial.println(luxValue, 1);
  }
  
  // Publication toutes les 10 secondes
  if (now - lastPublish >= PUBLISH_INTERVAL && sampleCount == 10) {
    lastPublish = now;
    
    // Moyenne des 10 dernières valeurs
    float sum = 0;
    for (int i = 0; i < 10; i++) {
      sum += luxValues[i];
    }
    float moyenneLux = sum / 10.0;
    
    publishData(moyenneLux, false);  // heartbeat=false (données normales)
  }
}