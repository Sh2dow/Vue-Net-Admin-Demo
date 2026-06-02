import psycopg2
conn = psycopg2.connect(host='localhost', port=5432, dbname='vue_demo_auth', user='vue', password='123')
cur = conn.cursor()
cur.execute("SELECT u.username FROM user_entity u JOIN realm r ON u.realm_id=r.id WHERE r.name='myrealm' LIMIT 10")
print([r[0] for r in cur.fetchall()])
conn.close()
