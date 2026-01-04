#!/usr/bin/env bash
set -eu

source .env.default
[[ -f ".env" ]] && source ".env"


echo "To change server/host, create a global .env"

pushd pgreflex-server > /dev/null
dotnet user-secrets set pgreflex:ConnectionString "Server=$HOST;Port=$PORT;UserId=$USER;Password=$PASSWORD;DATABASE=$DATABASE"

# time dotnet user-secrets set pgreflex:ConnectionString "Server=$HOST;Port=$PORT;UserId=$USER;Password=$PASSWORD;"
# real    0m0.423s
# user    0m0.482s
# sys     0m0.079s
# Are you alright microsoft?

popd > /dev/null

echo "DATABASE_URL=postgresql://$USER:$PASSWORD@$HOST:$PORT/$DATABASE" > packages/test-project/.env
echo "DATABASE_URL=postgresql://$USER:$PASSWORD@$HOST:$PORT/$DATABASE" > packages/update-creator/.env
