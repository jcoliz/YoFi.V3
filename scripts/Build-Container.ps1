#
# Build the Docker CI containers locally
#
# Useful for debugging CI build issues locally, and running functional tests locally.
#

docker compose -f ./docker/docker-compose-ci.yml build
