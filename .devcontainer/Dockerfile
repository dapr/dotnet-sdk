#
# Copyright 2021 The Dapr Authors
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#     http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

ARG VARIANT=bullseye
FROM mcr.microsoft.com/vscode/devcontainers/dotnet:dev-7.0-bullseye

# Install minikube
RUN MINIKUBE_URL="https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64" \
    && sudo curl -sSL -o /usr/local/bin/minikube "${MINIKUBE_URL}" \
    && sudo chmod 0755 /usr/local/bin/minikube \
    && MINIKUBE_SHA256=$(curl -sSL "${MINIKUBE_URL}.sha256") \
    && echo "${MINIKUBE_SHA256} */usr/local/bin/minikube" | sha256sum -c -


# Install Dapr CLI
RUN wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash

# Install Azure Dev CLI
RUN curl -fsSL https://aka.ms/install-azd.sh | bash