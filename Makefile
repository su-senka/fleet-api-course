# Convenience targets. Everything here is a docker compose or dotnet command you could type
# yourself; nothing is hidden. Windows users without make: the equivalent PowerShell one-liners
# are in the README.

COMPOSE := docker compose

.DEFAULT_GOAL := help

.PHONY: help
help: ## Show this help
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-14s\033[0m %s\n", $$1, $$2}'

.PHONY: up
up: ## Start the infrastructure and wait for it to be healthy
	$(COMPOSE) up -d --wait
	@$(MAKE) --no-print-directory ports

.PHONY: down
down: ## Stop the infrastructure, keeping the data
	$(COMPOSE) down

.PHONY: reset
reset: ## Destroy every volume, start clean, then migrate and reseed
	$(COMPOSE) down -v
	$(COMPOSE) up -d --wait
	@$(MAKE) --no-print-directory seed

.PHONY: seed
seed: ## Apply migrations and load the seed data
	@echo "Seeding arrives with the Vehicles and Drivers modules in milestone 2."

.PHONY: ports
ports: ## Show where everything is listening
	@echo ""
	@echo "  Postgres        localhost:5432      fleet / fleet / fleet"
	@echo "  Seq (logs)      http://localhost:8081"
	@echo "  Jaeger (traces) http://localhost:16686"
	@echo "  Keycloak        http://localhost:8080     admin / admin"
	@echo "  Azurite (blobs) localhost:10000"
	@echo "  Supplier (fake) http://localhost:5080/health"
	@echo ""

.PHONY: build
build: ## Build the whole solution
	dotnet build Fleet.sln

.PHONY: test
test: ## Run every test
	dotnet test Fleet.sln

.PHONY: api
api: ## Run the reference API on :5100
	dotnet run --project src/Fleet.Api

.PHONY: workshop
workshop: ## Run the workshop API on :5101
	dotnet run --project src/Fleet.Api.Workshop

.PHONY: logs
logs: ## Tail the infrastructure logs
	$(COMPOSE) logs -f
