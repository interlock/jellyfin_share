DOTNET = ~/.dotnet/dotnet
SRC_DIR = src
OUT_DIR = $(SRC_DIR)/bin/Release/net9.0/publish

TEST_DIR = tests/JellyfinMediaShare.Tests

.PHONY: build run clean install test

build:
	$(DOTNET) build $(SRC_DIR)/JellyfinMediaShare.csproj -c Release

test: build
	$(DOTNET) test $(TEST_DIR)/JellyfinMediaShare.Tests.csproj -c Release --logger "console;verbosity=normal"

publish:
	$(DOTNET) publish $(SRC_DIR)/JellyfinMediaShare.csproj -c Release -o $(SRC_DIR)/publish

clean:
	$(DOTNET) clean $(SRC_DIR)/JellyfinMediaShare.csproj -c Release
	rm -rf $(SRC_DIR)/bin $(SRC_DIR)/obj

install: publish
	cp $(OUT_DIR)/JellyfinMediaShare.dll ~/.config/jellyfin/plugins/