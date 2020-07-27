source $CONDA_SCRIPT

echo "Conda info:"
conda info

{{ if EnvironmentYmlFile | IsNotBlank }}
envFile="{{ EnvironmentYmlFile }}"
{{ else }}
envFile="oryx.environment.yml"
envFileTemplate="/opt/oryx/conda/{{ EnvironmentTemplateFileName }}"
sed 's/PYTHON_VERSION/{{ EnvironmentTemplatePythonVersion }}/g' "$envFileTemplate" > $envFile
{{ end }}

environmentPrefix="./venv"
echo
echo "Setting up Conda virtual environemnt..."
echo
START_TIME=$SECONDS
conda env create --file $envFile --prefix $environmentPrefix --quiet
ELAPSED_TIME=$(($SECONDS - $START_TIME))
echo "Done in $ELAPSED_TIME sec(s)."

{{ if HasRequirementsTxtFile }}
	echo
	echo "Activating environemnt..."
	conda activate $environmentPrefix

	echo
	echo "Running pip install..."
	echo
	pip install --no-cache-dir -r requirements.txt
{{ end }}