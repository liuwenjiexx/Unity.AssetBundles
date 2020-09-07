
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import com.unity3d.player.UnityPlayer;
import android.util.Log;



public class AssetBundleAndroidUtility {

    public static byte[] ReadStreamingAssetsAllBytes (String path) {

        InputStream inputStream = null;

        try {

            inputStream = UnityPlayer.currentActivity.getAssets().open(path);
            ByteArrayOutputStream outputStream = new ByteArrayOutputStream();
            byte buf[] = new byte[1024*4];
            int len;
            try {
                while ((len = inputStream.read(buf)) != -1) {
                    outputStream.write(buf, 0, len);
                }
                outputStream.close();
                inputStream.close();
    
            } catch (IOException e) {
    
            }
            return outputStream.toByteArray();
        } catch (IOException e) {

            Log.e("loadFile", e.getMessage());
            return null;
        }
    }
}
