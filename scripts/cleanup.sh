df -h
sudo du -sh /var/lib/docker
docker system df
docker compose down
docker system prune -af --volumes
sudo apt-get clean
sudo journalctl --vacuum-time=1d
df -h