netsh http delete urlacl url=http://127.0.0.1:62291/
netsh http delete urlacl url=http://localhost:62291/
netsh http delete urlacl url=http://*:62291/
netsh http add urlacl url=http://127.0.0.1:62291/ user=DURANDAL-PROD\logan
netsh http add urlacl url=http://localhost:62291/ user=DURANDAL-PROD\logan
netsh http add urlacl url=http://*:62291/ user=DURANDAL-PROD\logan

netsh http delete urlacl url=http://127.0.0.1:62297/
netsh http delete urlacl url=http://localhost:62297/
netsh http delete urlacl url=http://*:62297/
netsh http add urlacl url=http://127.0.0.1:62297/ user=DURANDAL-PROD\logan
netsh http add urlacl url=http://localhost:62297/ user=DURANDAL-PROD\logan
netsh http add urlacl url=http://*:62297/ user=DURANDAL-PROD\logan

netsh http delete urlacl url=https://127.0.0.1:62292/
netsh http delete urlacl url=https://localhost:62292/
netsh http delete urlacl url=https://*:62292/
netsh http add urlacl url=https://127.0.0.1:62292/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://localhost:62292/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://*:62292/ user=DURANDAL-PROD\logan
netsh http add sslcert ipport=0.0.0.0:62292 certhash=271a1ee53df110692763eb0a38ec191fff56165e appid="{a98456fe-46b3-464e-a074-17eab93ab607}"

netsh http delete urlacl url=https://127.0.0.1:62294/
netsh http delete urlacl url=https://localhost:62294/
netsh http delete urlacl url=https://*:62294/
netsh http add urlacl url=https://127.0.0.1:62294/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://localhost:62294/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://*:62294/ user=DURANDAL-PROD\logan
netsh http add sslcert ipport=0.0.0.0:62294 certhash=271a1ee53df110692763eb0a38ec191fff56165e appid="{38df3db7-9ec6-4181-9616-e07b1f4aff42}"

netsh http delete urlacl url=https://127.0.0.1:443/
netsh http delete urlacl url=https://localhost:443/
netsh http delete urlacl url=https://*:443/
netsh http add urlacl url=https://127.0.0.1:443/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://localhost:443/ user=DURANDAL-PROD\logan
netsh http add urlacl url=https://*:443/ user=DURANDAL-PROD\logan
netsh http add sslcert ipport=0.0.0.0:443 certhash=271a1ee53df110692763eb0a38ec191fff56165e appid="{38df3db7-9ec6-4181-9616-e07b1f4aff42}"
pause