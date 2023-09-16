using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using Cv2Point = OpenCvSharp.CPlusPlus.Point;
using Newtonsoft.Json;


namespace ParkingLot
{
    public partial class Form1 : Form
    {
        CvCapture capture;
        IplImage src;
        IplImage copy;
        bool MousePlus;
        public string pointsFilePath = "../../position.json";

        int width = 107;
        int height = 48;
        int threshold = 88; // 바이너리 초기 임계값

        public List<Cv2Point> posList;

        public List<Cv2Point> LoadPointsFromJson(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                MousePlus = false;
                return JsonConvert.DeserializeObject<List<Cv2Point>>(json);
            }
            else
            {
                MousePlus = true; // 마우스 왼쪽 클릭을 통해 위치값을 posList에 저장
                return new List<Cv2Point>();
            }
        }


        public Form1()
        {
            InitializeComponent();
            posList = LoadPointsFromJson(pointsFilePath); // 주차 칸의 왼쪽 상단 위치값 저장
            pictureBoxIpl1.MouseDown += PictureBoxIpl1_MouseDown; // 위치값이 저장된 파일이 없을 때 마우스 클릭로 주차칸 위치값 지정
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                capture = CvCapture.FromFile("../../carPark.mp4");

                // 바이너리 초기 임계값
                threshold = 88;

                // 트랙바 초기 위치 설정
                trackBar1.Value = threshold;
            }
            catch
            {
                timer1.Enabled = false;
            }
        }

        int frame_count = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            // 오른쪽 상단 label1에 프레임 수 표시
            frame_count++;
            label1.Text = frame_count.ToString() + "/" + capture.FrameCount.ToString();
            src = capture.QueryFrame();

            if (frame_count != capture.FrameCount)
            {
                if (copy == null)
                    copy = new IplImage(src.Size, BitDepth.U8, src.NChannels);
                Cv.Copy(src, copy);

                IplImage copyoutput = ProcessImage(copy, threshold);

                // label2에 (빈 주차칸 수 / 총 주차칸 수) 표시
                int emptySpaces = CheckEmptyParkingSpaces(copyoutput);
                label2.Text = $"Free: {emptySpaces}/{posList.Count}";

                pictureBoxIpl1.ImageIpl = src;
            }
            else
            {
                frame_count = 0;
                capture = CvCapture.FromFile("../../carPark.mp4");
            }
        }

        private void trackbar1_scroll(object sender, EventArgs e)
        {
            // 트랙바 값으로 threshold 업데이트
            threshold = trackBar1.Value;

            // 전처리를 수행하고 copy 영상 업데이트
            ProcessImage(copy, threshold);
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            // Label3에 바이너리 임계값 표시
            int threshold = trackBar1.Value;
            label3.Text = "Threshold: " + threshold.ToString();

        }

        public IplImage ProcessImage(IplImage input, int threshold)
        {
            if (input == null)
            {
                return null; // input이 null인 경우 메서드 실행 중지
            }

            IplImage imgGray = new IplImage(input.Size, BitDepth.U8, 1); // 그레이스케일 이미지를 생성
            IplImage imgBlur = new IplImage(input.Size, BitDepth.U8, 1);
            IplImage imgThreshold = new IplImage(input.Size, BitDepth.U8, 1);
            IplImage imgMorp = new IplImage(input.Size, BitDepth.U8, 1);

            Cv.CvtColor(input, imgGray, ColorConversion.BgrToGray); // 입력 이미지를 그레이스케일로 변환
            Cv.Smooth(imgGray, imgBlur, SmoothType.Blur, 9); // 블러
            Cv.Threshold(imgBlur, imgThreshold, threshold, 255, ThresholdType.Binary); // 바이너리

            IplConvKernel element = new IplConvKernel(5, 5, 2, 2, ElementShape.Custom, new int[3, 3]);
            Cv.Dilate(imgThreshold, imgMorp, element, 1); // 모폴로지 팽창을 통한 노이즈 감소

            // 전처리된 이미지 반환
            return imgMorp;
        }

        private int CheckEmptyParkingSpaces(IplImage input)
        {
            int emptySpaceCounter = 0;

            foreach (Cv2Point pos in posList)
            {
                int x = pos.X;
                int y = pos.Y;

                // 관심영역(ROI) 설정
                input.SetROI(new CvRect(x, y, width, height));

                // 주차 칸의 픽셀 수 계산
                int pixelCount = Cv.CountNonZero(input);

                // 픽셀 수가 임계값 이하인 경우 주차된 칸으로 간주
                if (pixelCount <= 4850) // 5136픽셀 = 107픽셀 * 48픽셀
                {
                    // 주차된 칸은 초록색으로 사각형 그리기
                    Cv.DrawRect(src, new CvRect(x, y, width, height), CvColor.Green, 1);
                }
                else
                {
                    // 주차되지 않은 칸은 노란색으로 사각형 그리기
                    Cv.DrawRect(src, new CvRect(x, y, width, height), CvColor.Yellow, 2);

                    // 주차되지 않은 칸의 수 카운팅
                    emptySpaceCounter++;
                }

                input.ResetROI(); // ROI 초기화
            }

            return emptySpaceCounter; // 주차되지 않은 칸의 수 반환
        }

        public void PictureBoxIpl1_MouseDown(object sender, MouseEventArgs e)
        {
            if (MousePlus == true)
            {
                if (e.Button == MouseButtons.Left)
                {
                    // 마우스 왼쪽 버튼 클릭 시, 클릭한 위치를 posList에 추가
                    posList.Add(new Cv2Point(e.X, e.Y));

                    // PictureBoxIpl1을 다시 그려서 클릭한 위치를 시각적으로 표시
                    pictureBoxIpl1.Invalidate();

                    if (posList.Count == 69)
                    {
                        // 위치값을 모두 받았을 때 position.json 파일로 저장
                        string json = JsonConvert.SerializeObject(posList);
                        File.WriteAllText(pointsFilePath, json);

                        // MousePlus를 false로 설정하여 마우스 클릭을 더 이상 받지 않도록 함
                        MousePlus = false;
                    }
                }
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 메모리 해제
            if (src != null)
                src.Dispose();
            if (copy != null)
                copy.Dispose();
        }

    }
}
