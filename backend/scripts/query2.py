import psycopg2
conn = psycopg2.connect(host='localhost', port=5432, dbname='vue_demo_auth', user='vue', password='123')
cur = conn.cursor()
cur.execute("SELECT c.client_id, c.public_client, c.direct_access_grants_enabled FROM client c JOIN realm r ON c.realm_id=r.id WHERE r.name='myrealm'")
for row in cur.fetchall():
    print(row)
conn.close()
