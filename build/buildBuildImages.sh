#!/bin/bash
# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license.
# --------------------------------------------------------------------------------------------

set -ex

declare -r REPO_DIR=$( cd $( dirname "$0" ) && cd .. && pwd )

# Load all variables
source $REPO_DIR/build/__variables.sh
source $REPO_DIR/build/__functions.sh
source $REPO_DIR/build/__sdkStorageConstants.sh
source $REPO_DIR/build/__nodeVersions.sh # for YARN_CACHE_BASE_TAG

cd "$BUILD_IMAGES_BUILD_CONTEXT_DIR"

declare BUILD_SIGNED=""

# Check to see if the build is by scheduled ORYX-CI or other azure devops build
# SIGNTYPE is set to 'real' on the Oryx-CI build definition itself (not in yaml file)
if [ "$SIGNTYPE" == "real" ] || [ "$SIGNTYPE" == "Real" ]
then
	# "SignType" will be real only for builds by scheduled and/or manual builds  of ORYX-CI
	BUILD_SIGNED="true"
else
	# locally we need to fake "binaries" directory to get a successful "copybuildscriptbinaries" build stage
    mkdir -p $BUILD_IMAGES_BUILD_CONTEXT_DIR/binaries
fi

# NOTE: We use only one label and put all information in it in order to limit the number of layers that are created
labelContent="git_commit=$GIT_COMMIT, build_number=$BUILD_NUMBER, release_tag_name=$RELEASE_TAG_NAME"

# https://medium.com/@Drew_Stokes/bash-argument-parsing-54f3b81a6a8f
PARAMS=""
while (( "$#" )); do
  case "$1" in
    -t|--type)
      imageTypeToBuild=$2
      shift 2
      ;;
    --) # end argument parsing
      shift
      break
      ;;
    -*|--*=) # unsupported flags
      echo "Error: Unsupported flag $1" >&2
      exit 1
      ;;
    *) # preserve positional arguments
      PARAMS="$PARAMS $1"
      shift
      ;;
  esac
done
# set positional arguments in their proper place
eval set -- "$PARAMS"

echo
echo "Image type to build is set to: $imageTypeToBuild"

function BuildAndTagStage()
{
	local dockerFile="$1"
	local stageName="$2"
	local stageTagName="$ACR_PUBLIC_PREFIX/$2"

	echo
	echo "Building stage '$stageName' with tag '$stageTagName'..."
	docker build \
		--target $stageName \
		-t $stageTagName \
		--label com.microsoft.oryx="$labelContent" \
		-f "$dockerFile" \
		.
}

function buildDockerImage() {
	local dockerFileToBuild="$1"
	local dockerImageRepoName="$2"
	local dockerFileForTestsToBuild="$3"
	local dockerImageForTestsRepoName="$4"
	local dockerImageForDevelopmentRepoName="$5"
	local dockerImageBaseTag="$6"

	# Tag stages to avoid creating dangling images.
	# NOTE:
	# These images are not written to artifacts file because they are not expected
	# to be pushed. This is just a workaround to prevent having dangling images so that
	# when a cleanup operation is being done on a build agent, a valuable dangling image
	# is not removed.
	BuildAndTagStage "$dockerFileToBuild" pre-final

	# If no tag was provided, use a default tag of "latest"
	if [ -z "$dockerImageBaseTag" ]
	then
		dockerImageBaseTag="latest"
	fi

	builtImageTag="$dockerImageRepoName:$dockerImageBaseTag"
	docker build -t $builtImageTag \
		--build-arg AI_KEY=$APPLICATION_INSIGHTS_INSTRUMENTATION_KEY \
		--build-arg SDK_STORAGE_BASE_URL_VALUE=$PROD_SDK_CDN_STORAGE_BASE_URL \
		--label com.microsoft.oryx="$labelContent" \
		-f "$dockerFileToBuild" \
		.

	echo
	echo Building a base image for tests...
	# Do not write this image tag to the artifacts file as we do not intend to push it
	testImageTag="$dockerImageForTestsRepoName:$dockerImageBaseTag"
	docker build -t $testImageTag -f "$dockerFileForTestsToBuild" .

	echo "$dockerImageRepoName:$dockerImageBaseTag" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE

	# Retag build image with build number tags
	if [ "$AGENT_BUILD" == "true" ]
	then
		uniqueTag="$BUILD_DEFINITIONNAME.$RELEASE_TAG_NAME"
		if [ "$dockerImageBaseTag" != "latest" ]
		then
			uniqueTag="$dockerImageBaseTag-$uniqueTag"
		fi

		echo
		echo "Retagging image '$builtImageTag' with ACR related tags..."
		docker tag "$builtImageTag" "$dockerImageRepoName:$dockerImageBaseTag"
		docker tag "$builtImageTag" "$dockerImageRepoName:$uniqueTag"

		# Write the list of images that were built to artifacts folder
		echo
		echo "Writing the list of build images built to artifacts folder..."

		# Write image list to artifacts file
		echo "$dockerImageRepoName:$uniqueTag" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	else
		docker tag "$builtImageTag" "$dockerImageForDevelopmentRepoName:$dockerImageBaseTag"
	fi
}

# Create artifact dir & files
mkdir -p "$ARTIFACTS_DIR/images"

touch $ACR_BUILD_IMAGES_ARTIFACTS_FILE
> $ACR_BUILD_IMAGES_ARTIFACTS_FILE

function createImageNameWithReleaseTag() {
	local imageNameToBeTaggedUniquely="$1"
	# Retag build image with build number tags
	if [ "$AGENT_BUILD" == "true" ]
	then
		local uniqueImageName="$imageNameToBeTaggedUniquely-$BUILD_DEFINITIONNAME.$RELEASE_TAG_NAME"

		echo
		echo "Retagging image '$imageNameToBeTaggedUniquely' with ACR related tags..."
		docker tag "$imageNameToBeTaggedUniquely" "$uniqueImageName"

		# Write image list to artifacts file
		echo "$uniqueImageName" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	fi
}

function buildGitHubRunnersBaseImage() {
	echo
	echo "-------------Building the image which uses GitHub runners' buildpackdeps-stretch specific digest----------------------------"
	docker build -t githubrunners-buildpackdeps-stretch \
		-f "$BUILD_IMAGES_GITHUB_RUNNERS_BUILDPACKDEPS_STRETCH_DOCKERFILE" \
		.
}

function buildTemporaryFilesImage() {
	buildGitHubRunnersBaseImage
	
	# Create the following image so that it's contents can be copied to the rest of the images below
	echo
	echo "-------------Creating temporary files image-------------------"
	docker build -t tempfiles \
		-f "$BUILD_IMAGES_TEMPORARY_FILES_DOCKERFILE" \
		.
}

function buildBuildScriptGeneratorImage() {
	buildTemporaryFilesImage

	# Create the following image so that it's contents can be copied to the rest of the images below
	echo
	echo "-------------Creating build script generator image-------------------"
	docker build -t buildscriptgenerator \
		--build-arg AGENTBUILD=$BUILD_SIGNED \
		--build-arg GIT_COMMIT=$GIT_COMMIT \
		--build-arg BUILD_NUMBER=$BUILD_NUMBER \
		--build-arg RELEASE_TAG_NAME=$RELEASE_TAG_NAME \
		-f "$BUILD_IMAGES_BUILDSCRIPTGENERATOR_DOCKERFILE" \
		.
}

function buildGitHubActionsImage() {
	buildBuildScriptGeneratorImage
	
	echo
	echo "-------------Creating build image for GitHub Actions-------------------"
	builtImageName="$ACR_BUILD_GITHUB_ACTIONS_IMAGE_NAME"
	docker build -t $builtImageName \
		--build-arg AI_KEY=$APPLICATION_INSIGHTS_INSTRUMENTATION_KEY \
		--build-arg SDK_STORAGE_BASE_URL_VALUE=$PROD_SDK_CDN_STORAGE_BASE_URL \
		--label com.microsoft.oryx="$labelContent" \
		-f "$BUILD_IMAGES_GITHUB_ACTIONS_DOCKERFILE" \
		.

	docker build \
		-t "$ORYXTESTS_BUILDIMAGE_REPO:github-actions" \
		-f "$ORYXTESTS_GITHUB_ACTIONS_BUILDIMAGE_DOCKERFILE" \
		.

	echo
	echo "$builtImageName" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	createImageNameWithReleaseTag $builtImageName
}

function buildJamStackImage() {
	buildGitHubActionsImage

	echo
	echo "-------------Creating AzureFunctions JamStack image-------------------"
	builtImageName="$ACR_AZURE_FUNCTIONS_JAMSTACK_IMAGE_NAME"
	docker build -t $builtImageName \
		--build-arg AI_KEY=$APPLICATION_INSIGHTS_INSTRUMENTATION_KEY \
		--build-arg SDK_STORAGE_BASE_URL_VALUE=$PROD_SDK_CDN_STORAGE_BASE_URL \
		--label com.microsoft.oryx="$labelContent" \
		-f "$BUILD_IMAGES_AZ_FUNCS_JAMSTACK_DOCKERFILE" \
		.
	echo
	echo "$builtImageName" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	createImageNameWithReleaseTag $builtImageName
}

function buildLtsVersionsImage() {
	buildBuildScriptGeneratorImage
	buildGitHubRunnersBaseImage

	echo
	echo "-------------Creating lts versions build image-------------------"
	buildDockerImage "$BUILD_IMAGES_LTS_VERSIONS_DOCKERFILE" \
					"$ACR_BUILD_IMAGES_REPO" \
					"$ORYXTESTS_LTS_VERSIONS_BUILDIMAGE_DOCKERFILE" \
					"$ORYXTESTS_BUILDIMAGE_REPO" \
					"$DEVBOX_BUILD_IMAGES_REPO" \
					"lts-versions"
}

function buildFullImage() {
	buildLtsVersionsImage

	# Pull and tag the image with the name that the full image's Dockerfile expects
	yarnImage="mcr.microsoft.com/oryx/base:build-yarn-cache-$YARN_CACHE_BASE_TAG"
	docker pull $yarnImage
	docker tag $yarnImage yarn-cache-base

	echo
	echo "-------------Creating full build image-------------------"
	buildDockerImage "$BUILD_IMAGES_DOCKERFILE" \
					"$ACR_BUILD_IMAGES_REPO" \
					"$ORYXTESTS_BUILDIMAGE_DOCKERFILE" \
					"$ORYXTESTS_BUILDIMAGE_REPO" \
					"$DEVBOX_BUILD_IMAGES_REPO"
}

function buildVsoImage() {
	buildFullImage

	echo
	echo "-------------Creating VSO build image-------------------"
	builtImageName="$ACR_BUILD_VSO_IMAGE_NAME"
	docker build -t $builtImageName \
		--build-arg AI_KEY=$APPLICATION_INSIGHTS_INSTRUMENTATION_KEY \
		--build-arg SDK_STORAGE_BASE_URL_VALUE=$PROD_SDK_CDN_STORAGE_BASE_URL \
		--label com.microsoft.oryx="$labelContent" \
		-f "$BUILD_IMAGES_VSO_DOCKERFILE" \
		.
	echo
	echo "$builtImageName" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	createImageNameWithReleaseTag $builtImageName
}

function buildCliImage() {
	buildBuildScriptGeneratorImage
	
	echo
	echo "-------------Creating CLI image-------------------"
	builtImageTag="$ACR_CLI_BUILD_IMAGE_REPO:latest"
	docker build -t $builtImageTag \
		--build-arg AI_KEY=$APPLICATION_INSIGHTS_INSTRUMENTATION_KEY \
		--build-arg SDK_STORAGE_BASE_URL_VALUE=$PROD_SDK_CDN_STORAGE_BASE_URL \
		--label com.microsoft.oryx="$labelContent" \
		-f "$BUILD_IMAGES_CLI_DOCKERFILE" \
		.
	echo
	echo "$builtImageTag" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE

	# Retag build image with build number tags
	if [ "$AGENT_BUILD" == "true" ]
	then
		uniqueTag="$BUILD_DEFINITIONNAME.$RELEASE_TAG_NAME"

		echo
		echo "Retagging image '$builtImageTag' with ACR related tags..."
		docker tag "$builtImageTag" "$ACR_CLI_BUILD_IMAGE_REPO:latest"
		docker tag "$builtImageTag" "$ACR_CLI_BUILD_IMAGE_REPO:$uniqueTag"

		# Write the list of images that were built to artifacts folder
		echo
		echo "Writing the list of build images built to artifacts folder..."

		# Write image list to artifacts file
		echo "$ACR_CLI_BUILD_IMAGE_REPO:$uniqueTag" >> $ACR_BUILD_IMAGES_ARTIFACTS_FILE
	else
		docker tag "$builtImageTag" "$DEVBOX_CLI_BUILD_IMAGE_REPO:latest"
	fi
}

function buildBuildPackImage() {
	# Build buildpack images
	# 'pack create-builder' is not supported on Windows
	if [[ "$OSTYPE" == "linux-gnu" ]] || [[ "$OSTYPE" == "darwin"* ]]; then
		source $REPO_DIR/build/buildBuildpacksImages.sh
	else
		echo
		echo "Skipping building Buildpacks images as platform '$OSTYPE' is not supported."
	fi
}

if [ -z "$imageTypeToBuild" ]; then
	buildGitHubActionsImage
	buildJamStackImage
	buildLtsVersionsImage
	buildFullImage
	buildVsoImage
	buildCliImage
	buildBuildPackImage
elif [ "$imageTypeToBuild" == "githubactions" ]; then
	buildGitHubActionsImage
elif [ "$imageTypeToBuild" == "jamstack" ]; then
	buildJamStackImage
elif [ "$imageTypeToBuild" == "ltsversions" ]; then
	buildLtsVersionsImage
elif [ "$imageTypeToBuild" == "full" ]; then
	buildFullImage
elif [ "$imageTypeToBuild" == "vso" ]; then
	buildVsoImage
elif [ "$imageTypeToBuild" == "cli" ]; then
	buildCliImage
elif [ "$imageTypeToBuild" == "buildpack" ]; then
	buildBuildPackImage
else
	echo "Error: Invalid value for '--type' switch. Valid values are: \
githubactions, jamstack, ltsversions, full, vso, cli, buildpack"
	exit 1
fi

echo
echo "List of images tagged (from '$ACR_BUILD_IMAGES_ARTIFACTS_FILE'):"
cat $ACR_BUILD_IMAGES_ARTIFACTS_FILE

echo
showDockerImageSizes

echo
dockerCleanupIfRequested

if [ -z "$BUILD_SIGNED" ]
then
	rm -rf binaries
fi

echo
echo "github-actions history"
docker history oryxdevmcr.azurecr.io/public/oryx/build:github-actions

echo
echo "Lts-versions history"
docker history oryxdevmcr.azurecr.io/public/oryx/build:lts-versions

echo
echo "Full image history"
docker history oryxdevmcr.azurecr.io/public/oryx/build
