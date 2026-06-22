import shutil, os, traceback
from gradio_client import Client, handle_file
IMG = "nanobanana-output/fullbody_frontfacing_orthographi.png"
OUT = "ai-model-gen"
print("connecting to TripoSR...")
c = Client("stabilityai/TripoSR", verbose=False)
print("preprocess (remove bg, isolate subject)...")
proc = c.predict(handle_file(IMG), True, 0.85, api_name="/preprocess")
print("  processed:", proc)
print("generate mesh (marching cubes res=256)... this runs on the Space GPU")
obj, glb = c.predict(handle_file(proc), 256, api_name="/generate")
print("  obj:", obj)
print("  glb:", glb)
for src, name in [(obj, "mudwretch_triposr.obj"), (glb, "mudwretch_triposr.glb")]:
    if src and os.path.exists(src):
        dst = os.path.join(OUT, name)
        shutil.copy(src, dst)
        print(f"  saved -> {dst} ({os.path.getsize(dst)} bytes)")
print("DONE")
