DOTNET = ~/.dotnet/dotnet
SRC_DIR = Jellyfin.Plugin.MediaShare
PUBLISH_DIR = $(SRC_DIR)/publish
DIST_DIR = dist
TEST_DIR = tests/JellyfinMediaShare.Tests

.PHONY: build clean install test publish package

build:
	$(DOTNET) build $(SRC_DIR)/Jellyfin.Plugin.MediaShare.csproj -c Release

test: build
	$(DOTNET) test $(TEST_DIR)/JellyfinMediaShare.Tests.csproj -c Release --logger "console;verbosity=normal"

publish:
	$(DOTNET) publish $(SRC_DIR)/$(SRC_DIR).csproj -c Release -o $(PUBLISH_DIR)

clean:
	$(DOTNET) clean $(SRC_DIR)/$(SRC_DIR).csproj -c Release
	rm -rf $(SRC_DIR)/bin $(SRC_DIR)/obj $(PUBLISH_DIR)

# Install built plugin to local Jellyfin plugin folder
install: publish
	mkdir -p ~/.config/jellyfin/plugins/MediaShare
	cp -rT $(PUBLISH_DIR) ~/.config/jellyfin/plugins/MediaShare/
	cp manifest.json ~/.config/jellyfin/plugins/MediaShare/

# Package a distributable copy of the plugin into dist/
package: publish
	rm -rf $(DIST_DIR) && mkdir -p $(DIST_DIR)
	cp -rT $(PUBLISH_DIR) $(DIST_DIR)
	cp manifest.json $(DIST_DIR)/
	cp README.md $(DIST_DIR)/
