#!/usr/bin/env python3
"""
Planificateur Jour/Nuit pour FiatLux
Version finale avec TLS mais sans vérification du hostname
"""

import paho.mqtt.client as mqtt
import schedule
import time
import json
import ssl
import tempfile
import os
from datetime import datetime

# ============================================================================
# CONFIGURATION
# ============================================================================

MQTT_BROKER = "10.0.0.67"
MQTT_PORT = 8883

# Salles à gérer
ROOMS = ["201","203"]  # Ajoute d'autres salles ici : ["201", "202", "203"]

# Plages horaires
HEURE_JOUR = "07:00"    # Début journée
HEURE_NUIT = "20:00"    # Début nuit

# ============================================================================
# CERTIFICAT CA
# ============================================================================

CA_CERT_CONTENT = """-----BEGIN CERTIFICATE-----
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
"""

# ============================================================================
# MQTT CLIENT
# ============================================================================

temp_cert_file = None

def setup_cert():
    """Crée un fichier temporaire avec le certificat"""
    global temp_cert_file
    temp_cert_file = tempfile.NamedTemporaryFile(mode='w', delete=False, suffix='.crt')
    temp_cert_file.write(CA_CERT_CONTENT)
    temp_cert_file.close()
    return temp_cert_file.name

cert_path = setup_cert()
print(f"✓ Certificat temporaire : {cert_path}")

client = mqtt.Client(client_id="scheduler_pc", callback_api_version=mqtt.CallbackAPIVersion.VERSION1)

def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✓ Connecté au broker MQTT")
    else:
        print(f"✗ Erreur connexion MQTT: {rc}")

def on_disconnect(client, userdata, rc):
    if rc != 0:
        print(f"⚠ Déconnexion inattendue : {rc}")

def on_publish(client, userdata, mid):
    print(f"  ✓ Message publié (mid: {mid})")

client.on_connect = on_connect
client.on_disconnect = on_disconnect
client.on_publish = on_publish

# Configuration TLS avec certificat MAIS sans vérification du hostname
try:
    client.tls_set(ca_certs=cert_path, cert_reqs=ssl.CERT_REQUIRED)
    client.tls_insecure_set(True)  # Ignore le mismatch IP vs hostname
    print("⚠ TLS activé avec certificat (hostname check désactivé)")
except Exception as e:
    print(f"✗ Erreur TLS : {e}")
    os.unlink(cert_path)
    exit(1)

# ============================================================================
# FONCTIONS PLANIFICATION
# ============================================================================

def publish_mode(mode):
    """Publie le mode jour/nuit sur MQTT pour toutes les salles"""
    payload = {
        "mode": mode,
        "timestamp": int(time.time() * 1000),
        "source": "scheduler"
    }
    
    json_payload = json.dumps(payload)
    
    # Publie pour chaque salle
    for room in ROOMS:
        topic = f"campus/config/{room}"
        result = client.publish(topic, json_payload, qos=1, retain=True)
        
        if result.rc == mqtt.MQTT_ERR_SUCCESS:
            print(f">>> {mode.upper()} → Room {room} [{datetime.now().strftime('%H:%M:%S')}]")
        else:
            print(f"✗ Erreur publication Room {room}: {result.rc}")
    
    print(f"    Payload: {json_payload}")

def activate_jour():
    """Active le mode JOUR (capteurs actifs)"""
    print("\n🌞 TRANSITION → JOUR")
    publish_mode("jour")

def activate_nuit():
    """Active le mode NUIT (capteurs en veille)"""
    print("\n🌙 TRANSITION → NUIT")
    publish_mode("nuit")

# ============================================================================
# MAIN
# ============================================================================

def main():
    print("=" * 60)
    print("PLANIFICATEUR JOUR/NUIT - FiatLux")
    print("=" * 60)
    print(f"Salles : {', '.join(ROOMS)}")
    print(f"Jour   : {HEURE_JOUR} - {HEURE_NUIT}")
    print(f"Nuit   : {HEURE_NUIT} - {HEURE_JOUR}")
    print(f"Broker : {MQTT_BROKER}:{MQTT_PORT}")
    print("=" * 60)
    
    # Connexion MQTT
    try:
        print(f"\nConnexion à {MQTT_BROKER}:{MQTT_PORT}...")
        client.connect(MQTT_BROKER, MQTT_PORT, 60)
        client.loop_start()
    except Exception as e:
        print(f"✗ Erreur connexion: {e}")
        os.unlink(cert_path)
        return
    
    # Attendre connexion
    print("Attente de la connexion...")
    time.sleep(3)
    
    # Détermine l'état initial
    now = datetime.now().time()
    heure_jour = datetime.strptime(HEURE_JOUR, "%H:%M").time()
    heure_nuit = datetime.strptime(HEURE_NUIT, "%H:%M").time()
    
    if heure_jour <= now < heure_nuit:
        print("\n✓ État actuel : JOUR")
        publish_mode("jour")
    else:
        print("\n✓ État actuel : NUIT")
        publish_mode("nuit")
    
    # Planification des transitions
    schedule.every().day.at(HEURE_JOUR).do(activate_jour)
    schedule.every().day.at(HEURE_NUIT).do(activate_nuit)
    
    print(f"\n✓ Planification active")
    print(f"  → Jour  à {HEURE_JOUR}")
    print(f"  → Nuit  à {HEURE_NUIT}")
    print("\n[Ctrl+C pour arrêter]\n")
    
    # Boucle principale
    try:
        while True:
            schedule.run_pending()
            time.sleep(60)
    except KeyboardInterrupt:
        print("\n\n✓ Arrêt du planificateur")
        client.loop_stop()
        client.disconnect()
        os.unlink(cert_path)
        print(f"✓ Certificat temporaire supprimé")

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"✗ Erreur fatale : {e}")
        import traceback
        traceback.print_exc()
        if temp_cert_file and os.path.exists(cert_path):
            os.unlink(cert_path)