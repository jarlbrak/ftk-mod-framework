# Exact endpoint SLERP regressions

`dotnet run --project tools/ai-model-pipeline/endpoint-rotation-tests -c Release`

Links the actual System-only runtime scalar implementation. Expected quaternions
were calculated independently with SciPy Rotation/Slerp from float input
quaternions. Cases cover the retained first live appearance frame37 neck endpoint
matrices/weight, coupled-axis separations 0.00001 through 170 degrees with three
intermediate weights, float output precision, opposite quaternion signs, endpoints,
normalization and malformed/nonfinite inputs. Exact scalar results use 2e-12;
float-boundary comparisons use 5e-8. This is not a Unity or visual test.

The first live report remains unchanged at
`scratch/kraken-endpoint-live-v1/appear.json`. Its observed Unity interpolation
failed the independent 1e-5 pose gate; neither that gate nor the verifier changed.
