FROM oryxdevmcr.azurecr.io/public/oryx/build AS main
ENV ORYX_PREFER_USER_INSTALLED_SDKS=true \
    PATH="$ORIGINAL_PATH:$ORYX_PATHS"
