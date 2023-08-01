# build: mono
# runtime: mono, zsh

mcs '-recurse:*.cs' -define:NO_FILES -out:installaur
chmod 755 ./installaur
sudo chown root:root ./installaur
sudo mv ./installaur /usr/local/bin